using Microsoft.Extensions.Logging;

using Shared.Background.Core.Base;
using Shared.Background.Core.Handlers;
using Shared.Background.Exceptions;
using Shared.Background.Settings;
using Shared.Extensions.Logging;
using Shared.Persistence.Abstractions.Entities;
using Shared.Persistence.Abstractions.Entities.Catalogs;
using Shared.Persistence.Abstractions.Repositories;

using System.Collections.Concurrent;

using static Shared.Background.Constants;
using static Shared.Persistence.Abstractions.Constants.Enums;

namespace Shared.Background.Core.BackgroundTasks;

public abstract class BackgroundTaskProcessing<TEntity, TStep> : BackgroundTaskBase<TStep>
    where TEntity : class, IPersistentProcess
    where TStep : class, IProcessStep
{
    private readonly SemaphoreSlim _semaphore = new(1);

    private readonly ILogger _logger;
    private readonly IPersistenceRepository<TEntity> _repository;
    private readonly BackgroundTaskStepHandler<TEntity> _handler;

    public BackgroundTaskProcessing(
        ILogger logger
        , IPersistenceRepository<TEntity> processRepository
        , IPersistenceRepository<TStep> catalogRepository
        , BackgroundTaskStepHandler<TEntity> handler) : base(logger, catalogRepository)
    {
        _logger = logger;
        _repository = processRepository;
        _handler = handler;
    }

    internal override async Task SuccessivelyHandleStepsAsync(Queue<TStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
    {
        for (var i = 0; i <= steps.Count; i++)
        {
            var step = steps.Dequeue();
            var action = step.Description ?? step.Name;

            TEntity[] processableData = await GetProcessableAsync(step, taskName, action, taskCount, settings, cToken);

            if (!processableData.Any())
                continue;

            await HandleDataAsync(step, taskName, action, processableData, settings.Steps.IsParallelProcessing, cToken);

            var isNextStep = steps.TryPeek(out var nextStep);

            try
            {
                _logger.LogTrace(taskName, action, Actions.ProcessableActions.StartSavingData);

                if (isNextStep)
                {
                    foreach (var entity in processableData.Where(x => x.ProcessStatusId == (int)ProcessStatuses.Processed))
                        entity.ProcessStatusId = (int)ProcessStatuses.Ready;

                    await _repository.Writer.SaveProcessableAsync(nextStep, processableData, cToken);

                    _logger.LogDebug(taskName, action, Actions.ProcessableActions.StopSavingData, $"The next step: '{nextStep!.Name}'");
                }
                else
                {
                    await _repository.Writer.SaveProcessableAsync(null, processableData, cToken);

                    _logger.LogDebug(taskName, action, Actions.ProcessableActions.StopSavingData);
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(new SharedBackgroundException(taskName, action, new(exception)));
            }
        }
    }
    internal override Task ParallelHandleStepsAsync(ConcurrentQueue<TStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
    {
        var tasks = Enumerable.Range(0, steps.Count).Select(x => ParallelHandleStepAsync(steps, taskName, taskCount, settings, cToken));
        return Task.WhenAll(tasks);
    }

    private async Task ParallelHandleStepAsync(ConcurrentQueue<TStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
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
            _logger.LogTrace(taskName, action, Actions.ProcessableActions.StartSavingData);

            await _semaphore.WaitAsync();
            await _repository.Writer.SaveProcessableAsync(null, processableData, cToken);
            _semaphore.Release();

            _logger.LogDebug(taskName, action, Actions.ProcessableActions.StopSavingData);
        }
        catch (Exception exception)
        {
            _logger.LogError(new SharedBackgroundException(taskName, action, new(exception)));
        }
    }

    private async Task<TEntity[]> GetProcessableAsync(TStep step, string taskName, string action, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
    {
        try
        {
            _logger.LogTrace(taskName, action, Actions.ProcessableActions.StartGettingProcessableData);

            var result = await _repository.Reader.GetProcessableAsync<TEntity>(step, settings.Steps.ProcessingMaxCount, cToken);

            if (settings.RetryPolicy is not null && taskCount % settings.RetryPolicy.EveryTime == 0)
            {
                _logger.LogTrace(taskName, action, Actions.ProcessableActions.StartGettingUnprocessableData);

                var retryTime = TimeOnly.Parse(settings.Scheduler.WorkTime).ToTimeSpan() * settings.RetryPolicy.EveryTime;
                var retryDate = DateTime.UtcNow.Add(-retryTime);

                var unprocessableResult = await _repository.Reader.GetUnprocessableAsync<TEntity>(step, settings.Steps.ProcessingMaxCount, retryDate, settings.RetryPolicy.MaxAttempts, cToken);

                if (unprocessableResult.Any())
                    result = result.Concat(unprocessableResult).ToArray();
            }

            _logger.LogDebug(taskName, action, Actions.ProcessableActions.StopGettingData, result.Length);

            return result;
        }
        catch (Exception exception)
        {
            throw new SharedBackgroundException(taskName, action, new(exception));
        }
    }
    private async Task HandleDataAsync(TStep step, string taskName, string action, TEntity[] data, bool isParallel, CancellationToken cToken)
    {
        try
        {
            _logger.LogTrace(taskName, action, Actions.ProcessableActions.StartHandlingData);

            if (!isParallel)
                await _handler.HandleProcessableStepAsync(step, data, cToken);
            else
            {
                await _semaphore.WaitAsync();
                await _handler.HandleProcessableStepAsync(step, data, cToken);
                _semaphore.Release();
            }

            foreach (var entity in data.Where(x => x.ProcessStatusId != (int)ProcessStatuses.Error))
                entity.ProcessStatusId = (int)ProcessStatuses.Processed;

            _logger.LogDebug(taskName, action, Actions.ProcessableActions.StopHandlingData);
        }
        catch (Exception exception)
        {
            foreach (var entity in data)
            {
                entity.ProcessStatusId = (int)ProcessStatuses.Error;
                entity.Error = exception.Message;
            }

            _logger.LogError(new SharedBackgroundException(taskName, action, new(exception)));
        }
    }
}