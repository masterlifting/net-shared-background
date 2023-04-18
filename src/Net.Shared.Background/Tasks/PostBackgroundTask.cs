using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using Net.Shared.Background.Core;
using Net.Shared.Background.Models.Exceptions;
using Net.Shared.Persistence.Abstractions.Entities;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;

using static Net.Shared.Background.Models.Constants.BackgroundTaskActions;
using static Net.Shared.Persistence.Models.Constants.Enums;

namespace Net.Shared.Background.Tasks;

public abstract class PostBackgroundTask<T> : BackgroundTask<T> where T : class, IPersistentProcess
{
    protected PostBackgroundTask(ILogger logger) : base(logger) => _logger = logger;

    #region PRIVATE FIELDS
    private readonly SemaphoreSlim _semaphore = new(1);
    private readonly ILogger _logger;
    #endregion

    #region OVERRIDED FUNCTIONS
    protected override async Task HandleSteps(Queue<IPersistentProcessStep> steps, BackgroundStepHandler<T> handler, CancellationToken cToken)
    {
        for (var i = 0; i <= steps.Count; i++)
        {
            var currentStep = steps.Dequeue();

            if (currentStep is null)
            {
                _logger.LogWarn("No steps to process.");
                return;
            }

            try
            {
                var data = await GetData(currentStep, cToken);

                if (!data.Any())
                    continue;

                await HandleData(currentStep, handler, data, cToken);

                steps.TryPeek(out var nextStep);

                await SaveResult(currentStep, nextStep, data, cToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(new NetSharedBackgroundException(exception));
            }
        }
    }
    protected override Task HandleStepsParallel(ConcurrentQueue<IPersistentProcessStep> steps, BackgroundStepHandler<T> handler, CancellationToken cToken)
    {
        var tasks = Enumerable.Range(0, steps.Count).Select(async _ =>
        {
            var isDequeue = steps.TryDequeue(out var currentStep);

            if (!isDequeue)
            {
                _logger.LogWarn("No steps to process.");
                return;
            }

            try
            {
                await _semaphore.WaitAsync(cToken);

                var data = await GetData(currentStep!, cToken);

                if (!data.Any())
                    return;

                await HandleData(currentStep!, handler, data, cToken);

                steps.TryPeek(out var nextStep);

                await SaveResult(currentStep!, nextStep, data, cToken);
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
    private async Task<T[]> GetData(IPersistentProcessStep step, CancellationToken cToken)
    {
        try
        {
            _logger.LogTrace(StartGettingProcessableData(TaskInfo.Name, step.Name));

            var processableData = await GetProcessableData(step, TaskInfo.Settings.Steps.ProcessingMaxCount, cToken);

            if (TaskInfo.Settings.RetryPolicy is not null && TaskInfo.Number % TaskInfo.Settings.RetryPolicy.EveryTime == 0)
            {
                _logger.LogTrace(StartGettingUnprocessableData(TaskInfo.Name, step.Name));

                var retryTime = TimeOnly.Parse(TaskInfo.Settings.Scheduler.WorkTime).ToTimeSpan() * TaskInfo.Settings.RetryPolicy.EveryTime;
                var retryDate = DateTime.UtcNow.Add(-retryTime);

                var unprocessableResult = await GetUnprocessableData(step, TaskInfo.Settings.Steps.ProcessingMaxCount, retryDate, TaskInfo.Settings.RetryPolicy.MaxAttempts, cToken);

                if (unprocessableResult.Any())
                    processableData = processableData.Concat(unprocessableResult).ToArray();
            }

            _logger.LogDebug(StopGettingData(TaskInfo.Name, step.Name));

            return processableData;
        }
        catch (Exception exception)
        {
            _logger.LogError(new NetSharedBackgroundException(exception));
            return Array.Empty<T>();
        }
    }
    private async Task HandleData(IPersistentProcessStep step, BackgroundStepHandler<T> handler, T[] data, CancellationToken cToken)
    {
        try
        {
            _logger.LogTrace(StartHandlingData(TaskInfo.Name, step.Name));

            await handler.Post(step, data, cToken);

            foreach (var item in data.Where(x => x.ProcessStatusId != (int)ProcessStatuses.Error))
                item.ProcessStatusId = (int)ProcessStatuses.Processed;

            _logger.LogDebug(StopHandlingData(TaskInfo.Name, step.Name));
        }
        catch (Exception exception)
        {
            foreach (var item in data)
            {
                item.ProcessStatusId = (int)ProcessStatuses.Error;
                item.Error = exception.Message;
            }

            _logger.LogError(new NetSharedBackgroundException(exception));
        }
    }
    #endregion
}