﻿using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using Net.Shared.Background.Abstractions.Interfaces;
using Net.Shared.Background.Abstractions.Models;
using Net.Shared.Background.Abstractions.Models.Exceptions;
using Net.Shared.Extensions;
using Net.Shared.Persistence.Abstractions.Interfaces.Entities;
using Net.Shared.Persistence.Abstractions.Interfaces.Entities.Catalogs;

using static Net.Shared.Persistence.Abstractions.Constants.Enums;

namespace Net.Shared.Background.Core;

public abstract class BackgroundTask<T> : IBackgroundTask where T : class, IPersistentProcess
{
    protected BackgroundTask(ILogger logger)
    {
        _logger = logger;
    }

    #region PRIVATE FIELDS
    private readonly SemaphoreSlim _semaphore = new(1);
    private readonly ILogger _logger;
    private BackgroundTaskModel taskModel = default!;
    #endregion

    #region PUBLIC FUNCTIONS
    public async Task Run(BackgroundTaskModel model, CancellationToken cToken)
    {
        taskModel = model;

        var steps = await GetQueueSteps(cToken);

        var handler = RegisterStepHandler();

        _logger.Trace($"The task '{model.Name}' № {model.Number} is started.");

        if (model.Settings.IsParallel)
            await HandleStepsParallel(new ConcurrentQueue<IPersistentProcessStep>(steps), handler, cToken);
        else
            await HandleSteps(steps, handler, cToken);
    }
    #endregion

    #region ABSTRACT FUNCTIONS
    protected abstract Task<T[]> GetProcessableData(IPersistentProcessStep step, int limit, CancellationToken cToken);
    protected abstract Task<IPersistentProcessStep[]> GetSteps(CancellationToken cToken);
    protected abstract IBackgroundTaskStep<T> RegisterStepHandler();
    protected abstract Task<T[]> GetUnprocessedData(IPersistentProcessStep step, int limit, DateTime updateTime, int maxAttempts, CancellationToken cToken);
    protected abstract Task SaveData(IPersistentProcessStep currentStep, IPersistentProcessStep? nextStep, IEnumerable<T> data, CancellationToken cToken);
    #endregion

    #region PRIVATE FUNCTIONS
    private async Task HandleSteps(Queue<IPersistentProcessStep> steps, IBackgroundTaskStep<T> handler, CancellationToken cToken)
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
                _logger.ErrorShort(new BackgroundException(exception));
            }
        }
    }
    private Task HandleStepsParallel(ConcurrentQueue<IPersistentProcessStep> steps, IBackgroundTaskStep<T> handler, CancellationToken cToken)
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
                _logger.ErrorShort(new BackgroundException(exception));
            }
        }, cToken);
    }

    private async Task<Queue<IPersistentProcessStep>> GetQueueSteps(CancellationToken cToken)
    {
        var steps = await GetSteps(cToken);

        var result = new Queue<IPersistentProcessStep>(taskModel.Settings.Steps.Length);

        var stepNames = steps.ToDictionary(x => x.Name);

        foreach (var stepName in taskModel.Settings.Steps)
        {
            if (stepNames.TryGetValue(stepName, out var step))
                result.Enqueue(step);
            else
                throw new BackgroundException($"The step '{stepName}' from the sattings was not found in the database.");
        }

        return result;
    }
    private async Task<T[]> GetData(IPersistentProcessStep step, CancellationToken cToken)
    {
        try
        {
            _logger.Trace($"Getting processable data for the task '{taskModel.Name}' by step '{step.Name}' is started.");

            var processableData = await GetProcessableData(step, taskModel.Settings.ChunkSize, cToken);

            _logger.Trace($"Getting processable data for the task '{taskModel.Name}' by step '{step.Name}' was done. Items count: {processableData.Length}.");

            if (taskModel.Settings.RetryPolicy is not null && taskModel.Number % taskModel.Settings.RetryPolicy.EveryTime == 0)
            {
                _logger.Trace($"Getting unprocessable data for the task '{taskModel.Name}' by step '{step.Name}' is started.");

                var retryTime = TimeOnly.Parse(taskModel.Settings.Schedule.WorkTime).ToTimeSpan() * taskModel.Settings.RetryPolicy.EveryTime;
                var retryDate = DateTime.UtcNow.Add(-retryTime);

                var unprocessableResult = await GetUnprocessedData(step, taskModel.Settings.ChunkSize, retryDate, taskModel.Settings.RetryPolicy.MaxAttempts, cToken);

                if (unprocessableResult.Any())
                {
                    _logger.Trace($"Getting unprocessable data for the task '{taskModel.Name}' by step '{step.Name}' was done. Items count: {processableData.Length}.");

                    processableData = processableData.Concat(unprocessableResult).ToArray();
                }
            }

            _logger.Trace($"Getting full data for the task '{taskModel.Name}' by step '{step.Name}' was done. Items count: {processableData.Length}.");

            return processableData;
        }
        catch (Exception exception)
        {
            throw new BackgroundException(exception);
        }
    }
    private async Task<T[]> HandleStep(IPersistentProcessStep step, IBackgroundTaskStep<T> handler, T[] data, CancellationToken cToken)
    {
        try
        {
            _logger.Trace($"Handling data for the task '{taskModel.Name}' by step '{step.Name}' is started.");

            var result = await handler.Handle(step, data, cToken);

            if (result.Errors.Any())
                throw new BackgroundException(result.GetError());

            foreach (var item in result.Data.Where(x => x.StatusId != (int)ProcessStatuses.Error))
                item.StatusId = (int)ProcessStatuses.Processed;

            _logger.Trace($"Handling data for the task '{taskModel.Name}' by step '{step.Name}' was done. Items count: {result.Data.Length}.");

            return result.Data;
        }
        catch (Exception exception)
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i].StatusId = (int)ProcessStatuses.Error;
                data[i].Error = exception.Message;
            }

            return data;
        }
    }
    private async Task SaveResult(IPersistentProcessStep currentStep, IPersistentProcessStep? nextStep, T[] data, CancellationToken cToken)
    {
        _logger.Trace($"Saving data for the task '{taskModel.Name}' by step '{currentStep.Name}' is started.");

        await SaveData(currentStep, nextStep, data, cToken);

        var saveResultMessage = $"Saving data for the task '{taskModel.Name}' by step '{currentStep.Name}' was done. Items count: {data.Length}.";

        if (nextStep is not null)
            saveResultMessage += $" Next step is '{nextStep.Name}'";

        _logger.Trace(saveResultMessage);

        var processedCount = data.Length;
        var unprocessedCount = 0;

        for (int i = 0; i < data.Length; i++)
        {
            if (data[i].StatusId == (int)ProcessStatuses.Error)
            {
                processedCount--;
                unprocessedCount++;
            }
        }

        _logger.Debug($"Task '{taskModel.Name}' with step '{currentStep.Name}' was done. Processed: {processedCount}. Unprocessed: {unprocessedCount}.");
    }
    #endregion
}
