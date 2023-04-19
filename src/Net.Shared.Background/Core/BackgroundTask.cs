using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;
using Net.Shared.Background.Models;
using Net.Shared.Background.Models.Exceptions;
using Net.Shared.Background.Models.Settings;
using Net.Shared.Persistence.Abstractions.Entities;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;

using static Net.Shared.Persistence.Models.Constants.Enums;
using Net.Shared.Background.Abstractions;

namespace Net.Shared.Background.Core;

public abstract class BackgroundTask<T> : IBackgroundTask where T : class, IPersistentProcess
{
    protected BackgroundTask(ILogger logger)
    {
        _logger = logger;
        TaskInfo = new(string.Empty, 0, new BackgroundTaskSettings());
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

        _logger.LogTrace($"Start task '{taskInfo.Name}' №{taskInfo.Number} with steps count: {steps.Count} as parallel: {taskInfo.Settings.Steps.IsParallelProcessing}.");

        if (taskInfo.Settings.Steps.IsParallelProcessing)
            await HandleStepsParallel(new ConcurrentQueue<IPersistentProcessStep>(steps), handler, cToken);
        else
            await HandleSteps(steps, handler, cToken);
    }
    #endregion

    #region ABSTRACT FUNCTIONS
    protected abstract Task<T[]> GetProcessableData(IPersistentProcessStep step, int limit, CancellationToken cToken = default);
    protected abstract Task<IPersistentProcessStep[]> GetSteps(CancellationToken cToken = default);
    protected abstract IBackgroundTaskHandler<T> RegisterTaskHandler();
    protected abstract Task<T[]> GetUnprocessableData(IPersistentProcessStep step, int limit, DateTime updateTime, int maxAttempts, CancellationToken cToken = default);
    protected abstract Task SaveData(IPersistentProcessStep? step, IEnumerable<T> entities, CancellationToken cToken = default);
    #endregion

    #region PRIVATE FUNCTIONS
    private async Task HandleSteps(Queue<IPersistentProcessStep> steps, IBackgroundTaskHandler<T> handler, CancellationToken cToken)
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

                data = await HandleStep(currentStep, handler, data, cToken);

                steps.TryPeek(out var nextStep);

                await SaveResult(currentStep, nextStep, data, cToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(new BackgroundException(exception));
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
                _logger.LogWarn("No steps to process.");
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
                _logger.LogError(new BackgroundException(exception));
            }
        }, cToken);
    }

    private async Task<Queue<IPersistentProcessStep>> GetQueueSteps(CancellationToken cToken = default)
    {
        var settingSteps = TaskInfo.Settings.Steps.Names;

        var steps = await GetSteps(cToken);

        var result = new Queue<IPersistentProcessStep>(settingSteps.Length);

        var stepNames = steps.ToDictionary(x => x.Name);

        foreach (var stepName in settingSteps)
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
            _logger.LogTrace($"Start getting processable data for the task: {TaskInfo.Name} by step: {step}.");

            var processableData = await GetProcessableData(step, TaskInfo.Settings.Steps.ProcessingMaxCount, cToken);

            if (TaskInfo.Settings.RetryPolicy is not null && TaskInfo.Number % TaskInfo.Settings.RetryPolicy.EveryTime == 0)
            {
                _logger.LogTrace($"Start getting unprocessable data for the task: {TaskInfo.Name} by step: {step}.");

                var retryTime = TimeOnly.Parse(TaskInfo.Settings.Scheduler.WorkTime).ToTimeSpan() * TaskInfo.Settings.RetryPolicy.EveryTime;
                var retryDate = DateTime.UtcNow.Add(-retryTime);

                var unprocessableResult = await GetUnprocessableData(step, TaskInfo.Settings.Steps.ProcessingMaxCount, retryDate, TaskInfo.Settings.RetryPolicy.MaxAttempts, cToken);

                if (unprocessableResult.Any())
                    processableData = processableData.Concat(unprocessableResult).ToArray();
            }

            _logger.LogDebug($"Stop getting data for the task: {TaskInfo.Name} by step: {step}.");

            return processableData;
        }
        catch (Exception exception)
        {
            _logger.LogError(new BackgroundException(exception));
            return Array.Empty<T>();
        }
    }
    private async Task<T[]> HandleStep(IPersistentProcessStep step, IBackgroundTaskHandler<T> handler, T[] data, CancellationToken cToken)
    {
        try
        {
            _logger.LogTrace($"Start handling data for the task: {TaskInfo.Name} by step: {step}.");

            var result = await handler.Handle(step, data, cToken);

            if(!result.IsSuccess)
                throw new BackgroundException(result.GetError());

            foreach (var item in result.Data.Where(x => x.ProcessStatusId != (int)ProcessStatuses.Error))
                item.ProcessStatusId = (int)ProcessStatuses.Processed;

            _logger.LogDebug($"Stop handling data for the task: {TaskInfo.Name} by step: {step}.");

            return result.Data;
        }
        catch (Exception exception)
        {
            foreach (var item in data)
            {
                item.ProcessStatusId = (int)ProcessStatuses.Error;
                item.Error = exception.Message;
            }

            _logger.LogError(new BackgroundException(exception));

            return data;
        }
    }
    private async Task SaveResult(IPersistentProcessStep currentStep, IPersistentProcessStep? nextStep, IEnumerable<T> data, CancellationToken cToken)
    {
        _logger.LogTrace($"Start saving data for the task: {TaskInfo.Name} by step: {currentStep}.");

        var stopMessage = $"Stop saving data for the task: {TaskInfo.Name} by step: {currentStep}.";

        if (nextStep is not null)
        {
            foreach (var item in data.Where(x => x.ProcessStatusId == (int)ProcessStatuses.Processed))
                item.ProcessStatusId = (int)ProcessStatuses.Ready;

            stopMessage += $" Next step: '{nextStep.Name}'";
        }

        await SaveData(nextStep, data, cToken);

        _logger.LogDebug(stopMessage);
    }
    #endregion
}