using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using Net.Shared.Background.Core;
using Net.Shared.Background.Handlers;
using Net.Shared.Background.Models.Exceptions;
using Net.Shared.Persistence.Abstractions.Entities;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;

using static Net.Shared.Background.Models.Constants.BackgroundTaskActions;
using static Net.Shared.Persistence.Models.Constants.Enums;

namespace Net.Shared.Background.BackgroundTasks;

public abstract class ProcessingBackgroundTask<T> : NetSharedBackgroundProcessTask where T : class, IPersistentProcess
{
    protected ProcessingBackgroundTask(ILogger logger) : base(logger)
    {
        _logger = logger;
        _handler = RegisterProcessStepHandlers<T>();
    }

    #region PRIVATE FIELDS
    private readonly SemaphoreSlim _semaphore = new(1);
    private readonly ILogger _logger;
    private readonly BackgroundProcessStepHandler<T> _handler;
    #endregion

    #region OVERRIDED FUNCTIONS
    protected override async Task HandleSteps(Queue<IPersistentProcessStep> steps, CancellationToken cToken)
    {
        for (var i = 0; i <= steps.Count; i++)
        {
            var step = steps.Dequeue();

            var processableData = await GetProcessableData(step, cToken);

            if (!processableData.Any())
                continue;

            await HandleData(step, processableData, cToken);

            var isNextStep = steps.TryPeek(out var nextStep);

            try
            {
                _logger.LogTrace(StartSavingData(Info.Name, step.Name));

                if (isNextStep)
                {
                    foreach (var entity in processableData.Where(x => x.ProcessStatusId == (int)ProcessStatuses.Processed))
                        entity.ProcessStatusId = (int)ProcessStatuses.Ready;

                    await SetProcessableData(nextStep, processableData, cToken);

                    _logger.LogDebug(StopSavingData(Info.Name, step.Name) + $" Next step: '{nextStep!.Name}'");
                }
                else
                {
                    await SetProcessableData(null, processableData, cToken);

                    _logger.LogDebug(StopSavingData(Info.Name, step.Name));
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(new NetSharedBackgroundException(exception));
            }
        }
    }
    protected override Task HandleStepsParallel(ConcurrentQueue<IPersistentProcessStep> steps, CancellationToken cToken)
    {
        var tasks = Enumerable.Range(0, steps.Count).Select(async _ =>
        {
            var isDequeue = steps.TryDequeue(out var step);

            if (!isDequeue)
            {
                _logger.LogWarn($"No steps to process by step {step?.Name}.");
                return;
            }

            try
            {
                await _semaphore.WaitAsync(cToken);

                var processableData = await GetProcessableData(step!, cToken);

                if (!processableData.Any())
                    return;

                await HandleData(step!, processableData, cToken);

                var isNextStep = steps.TryPeek(out var nextStep);

                _logger.LogTrace(StartSavingData(Info.Name, step!.Name));

                if (isNextStep)
                {
                    foreach (var entity in processableData.Where(x => x.ProcessStatusId == (int)ProcessStatuses.Processed))
                        entity.ProcessStatusId = (int)ProcessStatuses.Ready;

                    await SetProcessableData(nextStep, processableData, cToken);

                    _logger.LogDebug(StopSavingData(Info.Name, step.Name) + $" Next step: '{nextStep!.Name}'");
                }
                else
                {
                    await SetProcessableData(null, processableData, cToken);

                    _logger.LogDebug(StopSavingData(Info.Name, step.Name));
                }

                _logger.LogDebug(StopSavingData(Info.Name, step!.Name));
            }
            catch
            {
                throw;
            }
            finally
            {
                _semaphore.Release();
            }
        });

        return Task.WhenAll(tasks).ContinueWith(task =>
         {
             if (task.IsFaulted)
             {
                 var exception = task.Exception ?? new AggregateException("Unhandled exception of the paralel task.");
                 _logger.LogError(new NetSharedBackgroundException(exception));
             }
         }, cToken);
    }
    #endregion

    #region PRIVATE FUNCTIONS
    private async Task<T[]> GetProcessableData(IPersistentProcessStep step, CancellationToken cToken)
    {
        try
        {
            _logger.LogTrace(StartGettingProcessableData(Info.Name, step.Name));

            var result = await GetProcessableData<T>(step, Info.Settings.Steps.ProcessingMaxCount, cToken);

            if (Info.Settings.RetryPolicy is not null && Info.Number % Info.Settings.RetryPolicy.EveryTime == 0)
            {
                _logger.LogTrace(StartGettingUnprocessableData(Info.Name, step.Name));

                var retryTime = TimeOnly.Parse(Info.Settings.Scheduler.WorkTime).ToTimeSpan() * Info.Settings.RetryPolicy.EveryTime;
                var retryDate = DateTime.UtcNow.Add(-retryTime);

                var unprocessableResult = await GetUnprocessableData<T>(step, Info.Settings.Steps.ProcessingMaxCount, retryDate, Info.Settings.RetryPolicy.MaxAttempts, cToken);

                if (unprocessableResult.Any())
                    result = result.Concat(unprocessableResult).ToArray();
            }

            _logger.LogDebug(StopGettingData(Info.Name, step.Name));

            return result;
        }
        catch (Exception exception)
        {
            _logger.LogError(new NetSharedBackgroundException(exception));
            return Array.Empty<T>();
        }
    }
    private async Task HandleData(IPersistentProcessStep step, T[] data, CancellationToken cToken)
    {
        try
        {
            _logger.LogTrace(StartHandlingData(Info.Name, step.Name));

            await _handler.HandleStep(step, data, cToken);

            foreach (var entity in data.Where(x => x.ProcessStatusId != (int)ProcessStatuses.Error))
                entity.ProcessStatusId = (int)ProcessStatuses.Processed;

            _logger.LogDebug(StopHandlingData(Info.Name, step.Name));
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
    #endregion
}