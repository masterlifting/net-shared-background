using Microsoft.Extensions.Logging;

using Net.Shared.Background.Core;
using Net.Shared.Background.Handlers;
using Net.Shared.Background.Models.Exceptions;
using Net.Shared.Background.Models.Settings;
using Net.Shared.Persistence.Abstractions.Entities;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;
using Net.Shared.Persistence.Abstractions.Repositories;

using System.Collections.Concurrent;

using static Net.Shared.Background.Models.Constants.BackgroundTaskActions;
using static Net.Shared.Persistence.Models.Constants.Enums;

namespace Net.Shared.Background.BackgroundTasks;

public abstract class ProcessingBackgroundTask<TProcess, TProcessStep> : NetSharedBackgroundTask<TProcessStep>
    where TProcess : class, IPersistentProcess
    where TProcessStep : class, IPersistentProcessStep
{
    private readonly SemaphoreSlim _semaphore = new(1);

    private readonly ILogger _logger;
    private readonly IPersistenceRepository<TProcess> _processRepository;
    private readonly BackgroundTaskHandler<TProcess> _handler;

    protected ProcessingBackgroundTask(
        ILogger logger
        , IPersistenceRepository<TProcess> processRepository
        , IPersistenceRepository<TProcessStep> processStepRepository
        , BackgroundTaskHandler<TProcess> handler) : base(logger, processStepRepository)
    {
        _logger = logger;
        _processRepository = processRepository;
        _handler = handler;
    }

    internal override async Task HandleSteps(Queue<TProcessStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
    {
        for (var i = 0; i <= steps.Count; i++)
        {
            var step = steps.Dequeue();

            var action = step.Description ?? step.Name;

            var processableData = await GetProcessable(taskName, taskCount, step , settings, cToken);

            if (!processableData.Any())
                continue;

            await HandleData(taskName, step, processableData, settings.Steps.IsParallelProcessing, cToken);

            var isNextStep = steps.TryPeek(out var nextStep);

            try
            {
                _logger.LogTrace(StartSavingData(taskName));

                if (isNextStep)
                {
                    foreach (var entity in processableData.Where(x => x.ProcessStatusId == (int)ProcessStatuses.Processed))
                        entity.ProcessStatusId = (int)ProcessStatuses.Ready;

                    await _processRepository.Writer.SaveProcessable(nextStep, processableData, cToken);

                    _logger.LogDebug(StopSavingData(taskName) + $". The next step is '{nextStep!.Name}'");
                }
                else
                {
                    await _processRepository.Writer.SaveProcessable(null, processableData, cToken);

                    _logger.LogDebug(StopSavingData(taskName));
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(new NetSharedBackgroundException(exception));
            }
        }
    }
    internal override Task HandleStepsParallel(ConcurrentQueue<TProcessStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
    {
        var tasks = Enumerable.Range(0, steps.Count).Select(x => HandleStepParallel(steps, taskName, taskCount, settings, cToken));
        return Task.WhenAll(tasks);
    }

    private async Task HandleStepParallel(ConcurrentQueue<TProcessStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
    {
        var isDequeue = steps.TryDequeue(out var step);

        if (steps.Any() && !isDequeue)
            await HandleStepParallel(steps, taskName, taskCount, settings, cToken);

        var processableData = await GetProcessable(taskName, taskCount, step, settings, cToken);

        if (!processableData.Any())
            return;

        await HandleData(taskName, step, processableData, settings.Steps.IsParallelProcessing, cToken);

        try
        {
            _logger.LogTrace(StartSavingData(taskName));

            await _semaphore.WaitAsync(cToken);

            await _processRepository.Writer.SaveProcessable(null, processableData, cToken);
            
            _semaphore.Release();

            _logger.LogDebug(StopSavingData(taskName));
        }
        catch (Exception exception)
        {
            _logger.LogError(new NetSharedBackgroundException(exception));
        }
    }

    private async Task<TProcess[]> GetProcessable(string taskName, int taskCount, TProcessStep step, BackgroundTaskSettings settings, CancellationToken cToken)
    {
        try
        {
            _logger.LogTrace(StartGettingProcessableData(taskName));

            var result = await _processRepository.Reader.GetProcessableAsync<TProcess>(step, settings.Steps.ProcessingMaxCount, cToken);

            if (settings.RetryPolicy is not null && taskCount % settings.RetryPolicy.EveryTime == 0)
            {
                _logger.LogTrace(StartGettingUnprocessableData(taskName));

                var retryTime = TimeOnly.Parse(settings.Scheduler.WorkTime).ToTimeSpan() * settings.RetryPolicy.EveryTime;
                var retryDate = DateTime.UtcNow.Add(-retryTime);

                var unprocessableResult = await _processRepository.Reader.GetUnprocessableAsync<TProcess>(step, settings.Steps.ProcessingMaxCount, retryDate, settings.RetryPolicy.MaxAttempts, cToken);

                if (unprocessableResult.Any())
                    result = result.Concat(unprocessableResult).ToArray();
            }

            _logger.LogDebug(StopGettingData(taskName));

            return result;
        }
        catch (Exception exception)
        {
            throw new NetSharedBackgroundException(exception);
        }
    }
    private async Task HandleData(string taskName, TProcessStep step, TProcess[] data, bool isParallel, CancellationToken cToken)
    {
        try
        {
            _logger.LogTrace(StartHandlingData(taskName));

            if (!isParallel)
                await _handler.HandleStep(step, data, cToken);
            else
            {
                await _semaphore.WaitAsync(cToken);
                
                await _handler.HandleStep(step, data, cToken);
                
                _semaphore.Release();
            }

            foreach (var entity in data.Where(x => x.ProcessStatusId != (int)ProcessStatuses.Error))
                entity.ProcessStatusId = (int)ProcessStatuses.Processed;

            _logger.LogDebug(StopHandlingData(taskName));
        }
        catch (Exception exception)
        {
            foreach (var entity in data)
            {
                entity.ProcessStatusId = (int)ProcessStatuses.Error;
                entity.Error = exception.Message;
            }

            _logger.LogError(new NetSharedBackgroundException(exception));
        }
    }
}