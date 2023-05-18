using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Net.Shared.Background.Abstractions;
using Net.Shared.Background.Models;
using Net.Shared.Background.Models.Exceptions;
using Net.Shared.Background.Models.Settings;
using Net.Shared.Extensions.Logging;
using Net.Shared.Persistence.Abstractions.Entities;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;
using static Net.Shared.Persistence.Models.Constants.Enums;

namespace Net.Shared.Background.Core;

public abstract class BackgroundTask<T> : IBackgroundTask where T : class, IPersistentProcess
{
    protected BackgroundTask(ILogger logger)
    {
        _logger = logger;
        TaskInfo = new("Default", 0, new BackgroundTaskSettings());
    }

    #region PRIVATE FIELDS
    private readonly SemaphoreSlim _semaphore = new(1);
    private readonly ILogger _logger;
    #endregion

    #region PUBLIC PROPERTIES
    public BackgroundTaskInfo TaskInfo { get; private set; }
    #endregion

    #region PUBLIC FUNCTIONS
    public async Task Run(BackgroundTaskInfo taskInfo, CancellationToken cToken)
    {
        TaskInfo = taskInfo;

        var steps = await GetQueueSteps(cToken);

        var handler = RegisterTaskHandler();

        _logger.Trace($"The task '{taskInfo.Name}' № {taskInfo.Number} is started.");

        if (taskInfo.Settings.IsParallel)
            await HandleStepsParallel(new ConcurrentQueue<IPersistentProcessStep>(steps), handler, cToken);
        else
            await HandleSteps(steps, handler, cToken);
    }
    #endregion

    #region ABSTRACT FUNCTIONS
    protected abstract Task<T[]> GetProcessableData(IPersistentProcessStep step, int limit, CancellationToken cToken);
    protected abstract Task<IPersistentProcessStep[]> GetSteps(CancellationToken cToken);
    protected abstract IBackgroundTaskHandler<T> RegisterTaskHandler();
    protected abstract Task<T[]> GetUnprocessedData(IPersistentProcessStep step, int limit, DateTime updateTime, int maxAttempts, CancellationToken cToken);
    protected abstract Task SaveData(IPersistentProcessStep currentStep, IPersistentProcessStep? nextStep, IEnumerable<T> data, CancellationToken cToken);
    #endregion

    #region PRIVATE FUNCTIONS
    private async Task HandleSteps(Queue<IPersistentProcessStep> steps, IBackgroundTaskHandler<T> handler, CancellationToken cToken)
    {
        for (var i = 0; i <= steps.Count; i++)
        {
            var currentStep = steps.Dequeue();

            if (currentStep is null)
            {
                _logger.Warning("No steps to process.");
                return;
            }

            try
            {
                var data = await GetData(currentStep, cToken);

                if (!data.Any())
                    continue;

                data = await HandleStep(currentStep, handler, data, cToken);

                steps.TryPeek(out var nextStep);

                await SaveResult(currentStep, nextStep, data, cToken);
            }
            catch (Exception exception)
            {
                _logger.Error(new BackgroundException(exception));
            }
        }
    }
    private Task HandleStepsParallel(ConcurrentQueue<IPersistentProcessStep> steps, IBackgroundTaskHandler<T> handler, CancellationToken cToken)
    {
        var tasks = Enumerable.Range(0, steps.Count).Select(async _ =>
        {
            var isDequeue = steps.TryDequeue(out var currentStep);

            if (!isDequeue)
            {
                _logger.Warning("No steps to process.");
                return;
            }

            try
            {
                await _semaphore.WaitAsync(cToken);

                var data = await GetData(currentStep!, cToken);

                if (!data.Any())
                    return;

                data = await HandleStep(currentStep!, handler, data, cToken);

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
                _logger.Error(new BackgroundException(exception));
            }
        }, cToken);
    }

    private async Task<Queue<IPersistentProcessStep>> GetQueueSteps(CancellationToken cToken)
    {
        var steps = await GetSteps(cToken);

        var result = new Queue<IPersistentProcessStep>(TaskInfo.Settings.Steps.Length);

        var stepNames = steps.ToDictionary(x => x.Name);

        foreach (var stepName in TaskInfo.Settings.Steps)
        {
            if (stepNames.TryGetValue(stepName, out var step))
                result.Enqueue(step);
            else
                throw new BackgroundException($"The handler '{stepName}' from _options was not found in the database");
        }

        return result;
    }
    private async Task<T[]> GetData(IPersistentProcessStep step, CancellationToken cToken)
    {
        try
        {
            _logger.Trace($"Getting processable data for the task '{TaskInfo.Name}' by step '{step.Name}' is started.");

            var processableData = await GetProcessableData(step, TaskInfo.Settings.ChunkSize, cToken);

            _logger.Trace($"Getting processable data for the task '{TaskInfo.Name}' by step '{step.Name}' was succeeded. Items count: {processableData.Length}.");

            if (TaskInfo.Settings.RetryPolicy is not null && TaskInfo.Number % TaskInfo.Settings.RetryPolicy.EveryTime == 0)
            {
                _logger.Trace($"Getting unprocessable data for the task '{TaskInfo.Name}' by step '{step.Name}' is started.");

                var retryTime = TimeOnly.Parse(TaskInfo.Settings.Schedule.WorkTime).ToTimeSpan() * TaskInfo.Settings.RetryPolicy.EveryTime;
                var retryDate = DateTime.UtcNow.Add(-retryTime);

                var unprocessableResult = await GetUnprocessedData(step, TaskInfo.Settings.ChunkSize, retryDate, TaskInfo.Settings.RetryPolicy.MaxAttempts, cToken);

                if (unprocessableResult.Any())
                {
                    _logger.Trace($"Getting unprocessable data for the task '{TaskInfo.Name}' by step '{step.Name}' was succeeded. Items count: {processableData.Length}.");

                    processableData = processableData.Concat(unprocessableResult).ToArray();
                }
            }

            _logger.Debug($"Getting all the data for the task '{TaskInfo.Name}' by step '{step.Name}' was succeeded. Items count: {processableData.Length}.");

            return processableData;
        }
        catch (Exception exception)
        {
            _logger.Error(new BackgroundException(exception));
            return Array.Empty<T>();
        }
    }
    private async Task<T[]> HandleStep(IPersistentProcessStep step, IBackgroundTaskHandler<T> handler, T[] data, CancellationToken cToken)
    {
        try
        {
            _logger.Trace($"Handling data for the task '{TaskInfo.Name}' by step '{step.Name}' is started.");

            var result = await handler.Handle(step, data, cToken);

            if (!result.IsSuccess)
                throw new BackgroundException(result.GetError());

            foreach (var item in result.Data.Where(x => x.StatusId != (int)ProcessStatuses.Error))
                item.StatusId = (int)ProcessStatuses.Processed;

            _logger.Debug($"Handling data for the task '{TaskInfo.Name}' by step '{step.Name}' was succeeded. Items count: {result.Data.Length}.");

            return result.Data;
        }
        catch (Exception exception)
        {
            var backgroundException = new BackgroundException(exception);

            foreach (var item in data)
            {
                item.StatusId = (int)ProcessStatuses.Error;
                item.Error = backgroundException.Message;
            }

            _logger.Error(backgroundException);

            return data;
        }
    }
    private async Task SaveResult(IPersistentProcessStep currentStep, IPersistentProcessStep? nextStep, IEnumerable<T> data, CancellationToken cToken)
    {
        _logger.Trace($"Saving data for the task '{TaskInfo.Name}' by step '{currentStep.Name}' is started.");

        await SaveData(currentStep, nextStep, data, cToken);

        var saveResultMessage = $"Saving data for the task '{TaskInfo.Name}' by step '{currentStep.Name}' was succeeded. Items count: {data.Count()}.";

        if (nextStep is not null)
            saveResultMessage += $" Next step is '{nextStep.Name}'";

        _logger.Debug(saveResultMessage);
    }
    #endregion
}