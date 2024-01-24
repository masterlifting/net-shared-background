﻿using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using Net.Shared.Background.Abstractions.Interfaces;
using Net.Shared.Extensions.Logging;
using Net.Shared.Persistence.Abstractions.Interfaces.Entities;
using Net.Shared.Persistence.Abstractions.Interfaces.Entities.Catalogs;

using static Net.Shared.Persistence.Abstractions.Constants.Enums;

namespace Net.Shared.Background;

public abstract class BackgroundTask<T>(
    string taskName, 
    IBackgroundSettingsProvider settingsProvider, 
    ILogger logger
    ) : BackgroundService(taskName, settingsProvider, logger)
    where T : class, IPersistentProcess
{
    private readonly ILogger _log = logger;
    
    private readonly SemaphoreSlim _semaphore = new(1);

    protected override async Task Run(CancellationToken cToken)
    {
        var steps = await GetStepsQueue(cToken);

        var handler = GetStepHandler();

        _log.Trace($"Steps handling of the '{TaskName}' has started.");

        if (TaskSettings.IsParallel)
            await HandleStepsParallel(new ConcurrentQueue<IPersistentProcessStep>(steps), handler, cToken);
        else
            await HandleSteps(steps, handler, cToken);

        _log.Trace($"Steps handling of the '{TaskName}' has finished.");
    }

    #region ABSTRACT FUNCTIONS
    protected abstract Task<T[]> GetProcessableData(IPersistentProcessStep step, int limit, CancellationToken cToken);
    protected abstract Task<IPersistentProcessStep[]> GetSteps(CancellationToken cToken);
    protected abstract IBackgroundTaskStepHandler<T> GetStepHandler();
    protected abstract Task<T[]> GetUnprocessedData(IPersistentProcessStep step, int limit, DateTime updateTime, int maxAttempts, CancellationToken cToken);
    protected abstract Task SaveData(IPersistentProcessStep currentStep, IPersistentProcessStep? nextStep, IEnumerable<T> data, CancellationToken cToken);
    #endregion

    #region PRIVATE FUNCTIONS
    private async Task HandleSteps(Queue<IPersistentProcessStep> steps, IBackgroundTaskStepHandler<T> handler, CancellationToken cToken)
    {
        for (var i = 0; i <= steps.Count; i++)
        {
            var currentStep = steps.Dequeue();

            if (currentStep is null)
            {
                _log.Warn($"No steps to process for the '{TaskName}'.");
                return;
            }

            try
            {
                var data = await GetData(currentStep, cToken);

                if (data.Length == 0)
                    continue;

                data = await HandleStep(currentStep, handler, data, cToken);

                steps.TryPeek(out var nextStep);

                await SaveResult(currentStep, nextStep, data, cToken);
            }
            catch (Exception exception)
            {
                _log.ErrorCompact(exception);
            }
        }
    }
    private Task HandleStepsParallel(ConcurrentQueue<IPersistentProcessStep> steps, IBackgroundTaskStepHandler<T> handler, CancellationToken cToken)
    {
        var tasks = Enumerable.Range(0, steps.Count).Select(async _ =>
        {
            var isDequeue = steps.TryDequeue(out var currentStep);

            if (!isDequeue)
            {
                _log.Warn($"No steps to process for the '{TaskName}'.");
                return;
            }

            try
            {
                await _semaphore.WaitAsync(cToken);

                var data = await GetData(currentStep!, cToken);

                if (data.Length == 0)
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
                _log.ErrorCompact(task.Exception);
            }
        }, cToken);
    }

    private async Task<Queue<IPersistentProcessStep>> GetStepsQueue(CancellationToken cToken)
    {
        _log.Trace($"Getting steps for the '{TaskName}' has started.");

        var steps = await GetSteps(cToken);

        var result = new Queue<IPersistentProcessStep>(TaskSettings.Steps.Length);

        var stepNames = steps.ToDictionary(x => x.Name);

        foreach (var stepName in TaskSettings.Steps)
        {
            if (stepNames.TryGetValue(stepName, out var step))
                result.Enqueue(step);
            else
                throw new InvalidOperationException($"The step '{stepName}' was not found in the database.");
        }

        _log.Trace($"Getting steps for the '{TaskName}' has finished. Steps count: {result.Count}.");

        return result;
    }
    private async Task<T[]> GetData(IPersistentProcessStep step, CancellationToken cToken)
    {
        _log.Trace($"Getting processable data for the '{TaskName}' by step '{step.Name}' has started.");

        var processableData = await GetProcessableData(step, TaskSettings.ChunkSize, cToken);

        _log.Trace($"Getting processable data for the '{TaskName}' by step '{step.Name}' has finished. Items count: {processableData.Length}.");

        if (TaskSettings.RetryPolicy is not null && Count % TaskSettings.RetryPolicy.EveryTime == 0)
        {
            _log.Trace($"Getting unprocessable data for the '{TaskName}' by step '{step.Name}' has started.");

            var retryTime = TimeOnly.Parse(TaskSettings.Schedule.WorkTime).ToTimeSpan() * TaskSettings.RetryPolicy.EveryTime;
            var retryDate = DateTime.UtcNow.Add(-retryTime);

            var unprocessableResult = await GetUnprocessedData(step, TaskSettings.ChunkSize, retryDate, TaskSettings.RetryPolicy.MaxAttempts, cToken);

            _log.Trace($"Getting unprocessable data for the '{TaskName}' by step '{step.Name}' has finished. Items count: {processableData.Length}.");

            if (unprocessableResult.Length != 0)
                processableData = [.. processableData, .. unprocessableResult];
        }

        return processableData;
    }
    private async Task<T[]> HandleStep(IPersistentProcessStep step, IBackgroundTaskStepHandler<T> handler, T[] data, CancellationToken cToken)
    {
        try
        {
            _log.Trace($"Handling step '{step.Name}' for the '{TaskName}' has started.");

            var result = await handler.Handle(TaskName, step, data, cToken);

            if (result.Errors.Length != 0)
                throw new InvalidOperationException(result.GetError());

            foreach (var item in result.Data.Where(x => x.StatusId != (int)ProcessStatuses.Error))
                item.StatusId = (int)ProcessStatuses.Processed;

            _log.Trace($"Handling step '{step.Name}' for the '{TaskName}' has finished. Items count: {result.Data.Length}.");

            return result.Data;
        }
        catch (Exception exception)
        {
            for (var i = 0; i < data.Length; i++)
            {
                data[i].StatusId = (int)ProcessStatuses.Error;
                data[i].Error = exception.Message;
            }

            var error = $"Handling step '{step.Name}' for the '{TaskName}' has failed. Reason: {exception.Message}";

            _log.ErrorShort(new InvalidOperationException(error));

            return data;
        }
    }
    private async Task SaveResult(IPersistentProcessStep currentStep, IPersistentProcessStep? nextStep, T[] data, CancellationToken cToken)
    {
        _log.Trace($"Saving data for the '{TaskName}' by step '{currentStep.Name}' has started.");

        if (TaskSettings.IsInfinite && nextStep is null)
        {
            var steps = await GetStepsQueue(cToken);
            nextStep = steps.Peek();
        }

        await SaveData(currentStep, nextStep, data, cToken);

        var saveResultMessage = $"Saving data for the '{TaskName}' by step '{currentStep.Name}' has finished. Items count: {data.Length}.";

        if (nextStep is not null)
            saveResultMessage += $" Next step is '{nextStep.Name}'";

        _log.Trace(saveResultMessage);

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

        _log.Debug($"Task '{TaskName}' with step '{currentStep.Name}' has been done. Processed: {processedCount} ; Unprocessed: {unprocessedCount}.");
    }
    #endregion
}