using Microsoft.Extensions.Logging;

using Net.Shared.Background.Base;
using Net.Shared.Background.Handlers;
using Net.Shared.Background.Models.Exceptions;
using Net.Shared.Background.Models.Settings;
using Net.Shared.Extensions;
using Net.Shared.Persistence.Abstractions.Entities;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;
using Net.Shared.Persistence.Abstractions.Repositories;

using System.Collections.Concurrent;

using static Net.Shared.Background.Models.Constants.BackgroundTaskActions;
using static Net.Shared.Persistence.Models.Constants.Enums;

namespace Net.Shared.Background.BackgroundTasks;

public abstract class ProcessingBackgroundTask : NetSharedBackgroundTask
{
    private readonly SemaphoreSlim _semaphore = new(1);

    private readonly ILogger _logger;
    private readonly IPersistenceRepository<IPersistentProcess> _processRepository;
    private readonly BackgroundTaskHandler<IPersistentProcess> _handler;

    protected ProcessingBackgroundTask(
        ILogger logger
        , IPersistenceRepository<IPersistentProcess> processRepository
        , IPersistenceRepository<IPersistentProcessStep> processStepRepository
        , BackgroundTaskHandler<IPersistentProcess> handler) : base(logger, processStepRepository)
    {
        _logger = logger;
        _processRepository = processRepository;
        _handler = handler;
    }

    internal override async Task SuccessivelyHandleStepsAsync(Queue<IPersistentProcessStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
    {
        for (var i = 0; i <= steps.Count; i++)
        {
            var step = steps.Dequeue();
            var action = step.Description ?? step.Name;

            IPersistentProcess[] processableData = await GetProcessableAsync(step, taskName, action, taskCount, settings, cToken);

            if (!processableData.Any())
                continue;

            await HandleDataAsync(step, taskName, action, processableData, settings.Steps.IsParallelProcessing, cToken);

            var isNextStep = steps.TryPeek(out var nextStep);

            try
            {
                _logger.LogTrace(taskName, action, StartSavingData);

                if (isNextStep)
                {
                    foreach (var entity in processableData.Where(x => x.ProcessStatusId == (int)ProcessStatuses.Processed))
                        entity.ProcessStatusId = (int)ProcessStatuses.Ready;

                    await _processRepository.Writer.SaveProcessableAsync(nextStep, processableData, cToken);

                    _logger.LogDebug(taskName, action, StopSavingData, $"The next step: '{nextStep!.Name}'");
                }
                else
                {
                    await _processRepository.Writer.SaveProcessableAsync(null, processableData, cToken);

                    _logger.LogDebug(taskName, action, StopSavingData);
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(new NetSharedBackgroundException(exception));
            }
        }
    }
    internal override Task ParallelHandleStepsAsync(ConcurrentQueue<IPersistentProcessStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
    {
        var tasks = Enumerable.Range(0, steps.Count).Select(x => ParallelHandleStepAsync(steps, taskName, taskCount, settings, cToken));
        return Task.WhenAll(tasks);
    }

    private async Task ParallelHandleStepAsync(ConcurrentQueue<IPersistentProcessStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
    {
        var isDequeue = steps.TryDequeue(out var step);

        if (steps.Any() && !isDequeue)
            await ParallelHandleStepAsync(steps, taskName, taskCount, settings, cToken);

        var action = step!.Description ?? step.Name;

        var processableData = await GetProcessableAsync(step, taskName, action, taskCount, settings, cToken);

        if (!processableData.Any())
            return;

        await HandleDataAsync(step, taskName, action, processableData, settings.Steps.IsParallelProcessing, cToken);

        try
        {
            _logger.LogTrace(taskName, action, StartSavingData);

            await _semaphore.WaitAsync();
            await _processRepository.Writer.SaveProcessableAsync(null, processableData, cToken);
            _semaphore.Release();

            _logger.LogDebug(taskName, action, StopSavingData);
        }
        catch (Exception exception)
        {
            _logger.LogError(new NetSharedBackgroundException(exception));
        }
    }

    private async Task<IPersistentProcess[]> GetProcessableAsync(IPersistentProcessStep step, string taskName, string action, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
    {
        try
        {
            _logger.LogTrace(taskName, action, StartGettingProcessableData);

            var result = await _processRepository.Reader.GetProcessableAsync<IPersistentProcess>(step, settings.Steps.ProcessingMaxCount, cToken);

            if (settings.RetryPolicy is not null && taskCount % settings.RetryPolicy.EveryTime == 0)
            {
                _logger.LogTrace(taskName, action, StartGettingUnprocessableData);

                var retryTime = TimeOnly.Parse(settings.Scheduler.WorkTime).ToTimeSpan() * settings.RetryPolicy.EveryTime;
                var retryDate = DateTime.UtcNow.Add(-retryTime);

                var unprocessableResult = await _processRepository.Reader.GetUnprocessableAsync<IPersistentProcess>(step, settings.Steps.ProcessingMaxCount, retryDate, settings.RetryPolicy.MaxAttempts, cToken);

                if (unprocessableResult.Any())
                    result = result.Concat(unprocessableResult).ToArray();
            }

            _logger.LogDebug(taskName, action, StopGettingData, result.Length);

            return result;
        }
        catch (Exception exception)
        {
            throw new NetSharedBackgroundException(exception);
        }
    }
    private async Task HandleDataAsync(IPersistentProcessStep step, string taskName, string action, IPersistentProcess[] data, bool isParallel, CancellationToken cToken)
    {
        try
        {
            _logger.LogTrace(taskName, action, StartHandlingData);

            if (!isParallel)
                await _handler.HandleStep(step, data, cToken);
            else
            {
                await _semaphore.WaitAsync();
                await _handler.HandleStep(step, data, cToken);
                _semaphore.Release();
            }

            foreach (var entity in data.Where(x => x.ProcessStatusId != (int)ProcessStatuses.Error))
                entity.ProcessStatusId = (int)ProcessStatuses.Processed;

            _logger.LogDebug(taskName, action, StopHandlingData);
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