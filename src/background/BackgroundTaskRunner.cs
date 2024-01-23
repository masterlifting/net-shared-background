using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;
using Net.Shared.Extensions.Logging;
using Net.Shared.Background.Abstractions.Interfaces;
using Net.Shared.Background.Abstractions.Models;
using Net.Shared.Persistence.Abstractions.Interfaces.Entities;
using Net.Shared.Persistence.Abstractions.Interfaces.Entities.Catalogs;

using static Net.Shared.Persistence.Abstractions.Constants.Enums;

namespace Net.Shared.Background;

public abstract class BackgroundTaskRunner<T>(ILogger logger) where T : class, IPersistentProcess
{
    private readonly ILogger _log = logger;
    
    private BackgroundTask _task = default!;
    private readonly SemaphoreSlim _semaphore = new(1);

    public async Task Run(BackgroundTask task, CancellationToken cToken)
    {
        _task = task;

        var steps = await GetQueueSteps(cToken);

        var handler = CreateStepHandler();

        _log.Trace($"Steps handling of the '{task.Name}' has started.");

        if (task.Settings.IsParallel)
            await HandleStepsParallel(new ConcurrentQueue<IPersistentProcessStep>(steps), handler, cToken);
        else
            await HandleSteps(steps, handler, cToken);

        _log.Trace($"Steps handling of the '{task.Name}' was done.");
    }

    #region ABSTRACT FUNCTIONS
    protected abstract Task<T[]> GetProcessableData(IPersistentProcessStep step, int limit, CancellationToken cToken);
    protected abstract Task<IPersistentProcessStep[]> GetSteps(CancellationToken cToken);
    protected abstract IBackgroundTaskStepHandler<T> CreateStepHandler();
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
                _log.Warn("No steps to process.");
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
                _log.Warn($"No steps to process of the '{_task.Name}'.");
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

    private async Task<Queue<IPersistentProcessStep>> GetQueueSteps(CancellationToken cToken)
    {
        var steps = await GetSteps(cToken);

        var result = new Queue<IPersistentProcessStep>(_task.Settings.Steps.Length);

        var stepNames = steps.ToDictionary(x => x.Name);

        foreach (var stepName in _task.Settings.Steps)
        {
            if (stepNames.TryGetValue(stepName, out var step))
                result.Enqueue(step);
            else
                throw new InvalidOperationException($"The step '{stepName}' from the settings was not found in the database.");
        }

        return result;
    }
    private async Task<T[]> GetData(IPersistentProcessStep step, CancellationToken cToken)
    {
        _log.Trace($"Getting processable data for the '{_task.Name}' by step '{step.Name}' is started.");

        var processableData = await GetProcessableData(step, _task.Settings.ChunkSize, cToken);

        _log.Trace($"Getting processable data for the '{_task.Name}' by step '{step.Name}' was done. Items count: {processableData.Length}.");

        if (_task.Settings.RetryPolicy is not null && _task.Count % _task.Settings.RetryPolicy.EveryTime == 0)
        {
            _log.Trace($"Getting unprocessable data for the '{_task.Name}' by step '{step.Name}' is started.");

            var retryTime = TimeOnly.Parse(_task.Settings.Schedule.WorkTime).ToTimeSpan() * _task.Settings.RetryPolicy.EveryTime;
            var retryDate = DateTime.UtcNow.Add(-retryTime);

            var unprocessableResult = await GetUnprocessedData(step, _task.Settings.ChunkSize, retryDate, _task.Settings.RetryPolicy.MaxAttempts, cToken);

            _log.Trace($"Getting unprocessable data for the '{_task.Name}' by step '{step.Name}' was done. Items count: {processableData.Length}.");

            if (unprocessableResult.Length != 0)
                processableData = [.. processableData, .. unprocessableResult];
        }

        return processableData;
    }
    private async Task<T[]> HandleStep(IPersistentProcessStep step, IBackgroundTaskStepHandler<T> handler, T[] data, CancellationToken cToken)
    {
        try
        {
            _log.Trace($"Handling step '{step.Name}' for the '{_task.Name}' is started.");

            var result = await handler.Handle(step, data, cToken);

            if (result.Errors.Length != 0)
                throw new InvalidOperationException(result.GetError());

            foreach (var item in result.Data.Where(x => x.StatusId != (int)ProcessStatuses.Error))
                item.StatusId = (int)ProcessStatuses.Processed;

            _log.Trace($"Handling step '{step.Name}' for the '{_task.Name}' was done. Items count: {result.Data.Length}.");

            return result.Data;
        }
        catch (Exception exception)
        {
            for (var i = 0; i < data.Length; i++)
            {
                data[i].StatusId = (int)ProcessStatuses.Error;
                data[i].Error = exception.Message;
            }

            return data;
        }
    }
    private async Task SaveResult(IPersistentProcessStep currentStep, IPersistentProcessStep? nextStep, T[] data, CancellationToken cToken)
    {
        _log.Trace($"Saving data for the '{_task.Name}' by step '{currentStep.Name}' is started.");

        if (_task.Settings.IsInfinite && nextStep is null)
        {
            var steps = await GetQueueSteps(cToken);
            nextStep = steps.Peek();
        }

        await SaveData(currentStep, nextStep, data, cToken);

        var saveResultMessage = $"Saving data for the '{_task.Name}' by step '{currentStep.Name}' was done. Items count: {data.Length}.";

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

        _log.Debug($"Task '{_task.Name}' with step '{currentStep.Name}' was done. Processed: {processedCount}. Unprocessed: {unprocessedCount}.");
    }
    #endregion
}
