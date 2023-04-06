using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Net.Shared.Background.Core;
using Net.Shared.Background.Handlers;
using Net.Shared.Background.Models.Exceptions;
using Net.Shared.Background.Models.Settings;
using Net.Shared.Persistence.Abstractions.Entities;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;
using Net.Shared.Persistence.Abstractions.Repositories;
using static Net.Shared.Background.Models.Constants.BackgroundTaskActions;
using static Net.Shared.Persistence.Models.Constants.Enums;

namespace Net.Shared.Background.BackgroundTasks;

public abstract class ProcessingBackgroundTask<TData> : NetSharedBackgroundProcessTask
    where TData : class, IPersistentProcess
{
    private readonly SemaphoreSlim _semaphore = new(1);

    private readonly ILogger _logger;
    private readonly BackgroundProcessTaskHandler<TData> _handler;
    private readonly IPersistenceProcessRepository _repository;

    protected ProcessingBackgroundTask(
        ILogger logger
        , IPersistenceProcessRepository repository
        , BackgroundProcessTaskHandler<TData> handler) : base(logger)
    {
        _logger = logger;
        _handler = handler;
        _repository = repository;
    }

    internal override async Task HandleSteps(Queue<IPersistentProcessStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
    {
        for (var i = 0; i <= steps.Count; i++)
        {
            var step = steps.Dequeue();

            var processableData = await GetProcessableData(taskName, taskCount, step, settings, cToken);

            if (!processableData.Any())
                continue;

            await HandleData(taskName, step, processableData, cToken);

            var isNextStep = steps.TryPeek(out var nextStep);

            try
            {
                _logger.LogTrace(StartSavingData(taskName, step.Name));

                if (isNextStep)
                {
                    foreach (var entity in processableData.Where(x => x.ProcessStatusId == (int)ProcessStatuses.Processed))
                        entity.ProcessStatusId = (int)ProcessStatuses.Ready;

                    await _repository.SetProcessableData(nextStep, processableData, cToken);

                    _logger.LogDebug(StopSavingData(taskName, step.Name) + $" Next step: '{nextStep!.Name}'");
                }
                else
                {
                    await _repository.SetProcessableData(null, processableData, cToken);

                    _logger.LogDebug(StopSavingData(taskName, step.Name));
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(new NetSharedBackgroundException(exception));
            }
        }
    }
    internal override Task HandleStepsParallel(ConcurrentQueue<IPersistentProcessStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
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

                var processableData = await GetProcessableData(taskName, taskCount, step!, settings, cToken);

                if (!processableData.Any())
                    return;

                await HandleData(taskName, step!, processableData, cToken);

                var isNextStep = steps.TryPeek(out var nextStep);

                _logger.LogTrace(StartSavingData(taskName, step!.Name));

                if (isNextStep)
                {
                    foreach (var entity in processableData.Where(x => x.ProcessStatusId == (int)ProcessStatuses.Processed))
                        entity.ProcessStatusId = (int)ProcessStatuses.Ready;

                    await _repository.SetProcessableData(nextStep, processableData, cToken);

                    _logger.LogDebug(StopSavingData(taskName, step.Name) + $" Next step: '{nextStep!.Name}'");
                }
                else
                {
                    await _repository.SetProcessableData(null, processableData, cToken);

                    _logger.LogDebug(StopSavingData(taskName, step.Name));
                }

                _logger.LogDebug(StopSavingData(taskName, step!.Name));
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

    private async Task<TData[]> GetProcessableData(string taskName, int taskCount, IPersistentProcessStep step, BackgroundTaskSettings settings, CancellationToken cToken)
    {
        try
        {
            _logger.LogTrace(StartGettingProcessableData(taskName, step.Name));

            var result = await _repository.GetProcessableData<TData>(step, settings.Steps.ProcessingMaxCount, cToken);

            if (settings.RetryPolicy is not null && taskCount % settings.RetryPolicy.EveryTime == 0)
            {
                _logger.LogTrace(StartGettingUnprocessableData(taskName, step.Name));

                var retryTime = TimeOnly.Parse(settings.Scheduler.WorkTime).ToTimeSpan() * settings.RetryPolicy.EveryTime;
                var retryDate = DateTime.UtcNow.Add(-retryTime);

                var unprocessableResult = await _repository.GetUnprocessableData<TData>(step, settings.Steps.ProcessingMaxCount, retryDate, settings.RetryPolicy.MaxAttempts, cToken);

                if (unprocessableResult.Any())
                    result = result.Concat(unprocessableResult).ToArray();
            }

            _logger.LogDebug(StopGettingData(taskName, step.Name));

            return result;
        }
        catch (Exception exception)
        {
            _logger.LogError(new NetSharedBackgroundException(exception));
            return Array.Empty<TData>();
        }
    }
    private async Task HandleData(string taskName, IPersistentProcessStep step, TData[] data, CancellationToken cToken)
    {
        try
        {
            _logger.LogTrace(StartHandlingData(taskName, step.Name));

            await _handler.HandleStep(step, data, cToken);

            foreach (var entity in data.Where(x => x.ProcessStatusId != (int)ProcessStatuses.Error))
                entity.ProcessStatusId = (int)ProcessStatuses.Processed;

            _logger.LogDebug(StopHandlingData(taskName, step.Name));
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