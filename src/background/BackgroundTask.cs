using System.Linq.Expressions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Net.Shared.Background.Abstractions.Interfaces;
using Net.Shared.Extensions.Logging;
using Net.Shared.Persistence.Abstractions.Interfaces.Entities;
using Net.Shared.Persistence.Abstractions.Interfaces.Entities.Catalogs;
using Net.Shared.Persistence.Abstractions.Interfaces.Repositories;

using static Net.Shared.Persistence.Abstractions.Constants.Enums;

namespace Net.Shared.Background;

public abstract class BackgroundTask<TData, TDataStep, TBackgroundStepHandler>(
    string taskName,
    Guid correlationId,
    ILogger logger,
    IServiceScopeFactory serviceScopeFactory,
    IBackgroundSettingsProvider settingsProvider
    ) : BackgroundService(taskName, settingsProvider, logger)
        where TData : class, IPersistentProcess
        where TDataStep : class, IPersistentProcessStep
        where TBackgroundStepHandler : class, IBackgroundTaskStepHandler<TData>
{
    private readonly Guid _correlationId = correlationId;
    private readonly TBackgroundStepHandler _handler = Activator.CreateInstance<TBackgroundStepHandler>();

    private readonly ILogger _log = logger;
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;

    protected override async Task Run(CancellationToken cToken)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();

        var processRepository = scope.ServiceProvider.GetRequiredService<IPersistenceProcessRepository<TData>>();
        var stepsRepository = scope.ServiceProvider.GetRequiredService<IPersistenceReaderRepository<TDataStep>>();

        var steps = await GetSteps(stepsRepository, cToken);

        await HandleSteps(scope.ServiceProvider, stepsRepository, processRepository, steps, cToken);
    }

    protected virtual Expression<Func<TData, bool>> DataFilter => x => true;

    private async Task<Queue<IPersistentProcessStep>> GetSteps(
        IPersistenceReaderRepository<TDataStep> repository,
        CancellationToken cToken)
    {
        _log.Trace($"Getting the steps for the '{TaskName}' has started.");

        var steps = await repository.FindMany<TDataStep>(new(), cToken);

        var taskSettingsSteps = TaskSettings.Steps.Split(',').Select(x => x.Trim()).ToArray();

        var result = new Queue<IPersistentProcessStep>(taskSettingsSteps.Length);

        var stepNames = steps.ToDictionary(x => x.Name);

        foreach (var stepName in taskSettingsSteps)
        {
            if (stepNames.TryGetValue(stepName, out var step))
                result.Enqueue(step);
            else
                throw new InvalidOperationException($"The step '{stepName}' was not found in the database.");
        }

        _log.Debug($"Getting the steps for the '{TaskName}' has finished. Steps count: {result.Count}.");

        return result;
    }
    private async Task HandleSteps(
        IServiceProvider serviceProvider,
        IPersistenceReaderRepository<TDataStep> stepsRepository,
        IPersistenceProcessRepository<TData> processRepository,
        Queue<IPersistentProcessStep> steps,
        CancellationToken cToken)
    {
        _log.Trace($"Steps handling of the '{TaskName}' has started.");

        for (var i = 0; i <= steps.Count; i++)
        {
            var currentStep = steps.Dequeue();

            if (currentStep is null)
            {
                _log.Warn($"No steps to process for the '{TaskName}'.");
                return;
            }

            _log.Info($"Task '{TaskName}' with step '{currentStep.Name}' has started.");

            var data = await GetStepData(processRepository, currentStep, cToken);

            if (data.Length == 0)
                continue;

            await HandleStep(serviceProvider, currentStep, data, cToken);

            steps.TryPeek(out var nextStep);

            await SaveStepData(stepsRepository, processRepository, currentStep, nextStep, data, cToken);
        }

        _log.Debug($"Steps handling of the '{TaskName}' has finished.");
    }


    private async Task<TData[]> GetStepData(
        IPersistenceProcessRepository<TData> repository,
        IPersistentProcessStep step,
        CancellationToken cToken)
    {
        _log.Trace($"Getting processable data for the '{TaskName}' by step '{step.Name}' has started.");

        TData[] processableData;

        try
        {
            processableData = await repository.GetProcessableData(_correlationId, step, TaskSettings.ChunkSize, DataFilter, cToken);
        }
        catch (Exception exception)
        {
            _log.ErrorShort(exception);
            return [];
        }

        _log.Debug($"Getting processable data for the '{TaskName}' by step '{step.Name}' has finished. Items count: {processableData.Length}.");

        if (TaskSettings.RetryPolicy is not null && RunCount % TaskSettings.RetryPolicy.EveryTime == 0)
        {
            _log.Trace($"Getting unprocessable data for the '{TaskName}' by step '{step.Name}' has started.");

            var retryTime = TimeOnly.Parse(TaskSettings.Schedule.WorkTime).ToTimeSpan() * TaskSettings.RetryPolicy.EveryTime;
            var retryDate = DateTime.UtcNow.Add(-retryTime);

            TData[] unprocessableData;

            try
            {
                unprocessableData = await repository.GetUnprocessedData(_correlationId, step, TaskSettings.ChunkSize, retryDate, TaskSettings.RetryPolicy.MaxAttempts, DataFilter, cToken);
            }
            catch (Exception exception)
            {
                _log.ErrorShort(exception);
                return processableData;
            }

            _log.Debug($"Getting unprocessable data for the '{TaskName}' by step '{step.Name}' has finished. Items count: {unprocessableData.Length}.");

            if (unprocessableData.Length != 0)
                processableData = [.. processableData, .. unprocessableData];
        }

        return processableData;
    }
    private async Task HandleStep(
        IServiceProvider serviceProvider,
        IPersistentProcessStep step,
        TData[] data,
        CancellationToken cToken)
    {
        _log.Trace($"Handling step '{step.Name}' for the '{TaskName}' has started.");

        try
        {
            await _handler.Handle(TaskName, serviceProvider, step, data, cToken);

            foreach (var item in data.Where(x => x.StatusId != (int)ProcessStatuses.Error))
                item.StatusId = (int)ProcessStatuses.Processed;

            _log.Debug($"Handling step '{step.Name}' for the '{TaskName}' has finished. Items count: {data.Length}.");
        }
        catch (Exception exception)
        {
            for (var i = 0; i < data.Length; i++)
            {
                data[i].StatusId = (int)ProcessStatuses.Error;
                data[i].Error = exception.Message;
            }

            var reason = exception.InnerException ?? exception;

            _log.Error($"Handling step '{step.Name}' for the '{TaskName}' has failed. Reason: {reason.Message}");
        }
    }
    private async Task SaveStepData(
        IPersistenceReaderRepository<TDataStep> stepsRepository,
        IPersistenceProcessRepository<TData> processRepository,
        IPersistentProcessStep currentStep,
        IPersistentProcessStep? nextStep,
        TData[] data,
        CancellationToken cToken)
    {
        _log.Trace($"Saving data for the '{TaskName}' by step '{currentStep.Name}' has started.");

        if (TaskSettings.IsInfinite && nextStep is null)
        {
            var steps = await GetSteps(stepsRepository, cToken);
            nextStep = steps.Peek();
        }

        await processRepository.SetProcessedData(_correlationId, currentStep, nextStep, data, cToken);

        var saveResultMessage = $"Saving data for the '{TaskName}' by step '{currentStep.Name}' has finished. Items count: {data.Length}.";

        if (nextStep is not null)
            saveResultMessage += $" Next step is '{nextStep.Name}'";

        _log.Debug(saveResultMessage);

        var processedCount = data.Length;
        var unprocessedCount = 0;

        for (var i = 0; i < data.Length; i++)
        {
            if (data[i].StatusId == (int)ProcessStatuses.Error)
            {
                processedCount--;
                unprocessedCount++;
            }
        }

        _log.Info($"Task '{TaskName}' with step '{currentStep.Name}' has been done. Processed: {processedCount} ; Unprocessed: {unprocessedCount}.");
    }
}
