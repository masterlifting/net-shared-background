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

namespace Shared.Background.Core.BackgroundTasks;

public abstract class BackgroundTaskPulling<TEntity, TStep> : BackgroundTaskBase<TStep>
    where TEntity : class,IPersistentProcess
    where TStep : class, IProcessStep
{
    private readonly SemaphoreSlim _semaphore = new(1);

    private readonly ILogger _logger;
    private readonly IPersistenceRepository<TEntity> _repository;
    private readonly BackgroundTaskStepHandler<TEntity> _handler;

    public BackgroundTaskPulling(
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

            var entities = await HandleDataAsync(step, taskName, action, settings.Steps.IsParallelProcessing, cToken);

            try
            {
                _logger.LogTrace(taskName, action, Actions.ProcessableActions.StartSavingData);

                await _repository.Writer.SaveProcessableAsync(null, entities, cToken);

                _logger.LogDebug(taskName, action, Actions.ProcessableActions.StopSavingData);
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

        var entities = await HandleDataAsync(step, taskName, action, settings.Steps.IsParallelProcessing, cToken);

        try
        {
            _logger.LogTrace(taskName, action, Actions.ProcessableActions.StartSavingData);

            await _semaphore.WaitAsync();
            await _repository.Writer.SaveProcessableAsync(null, entities, cToken);
            _semaphore.Release();

            _logger.LogDebug(taskName, action, Actions.ProcessableActions.StopSavingData);
        }
        catch (Exception exception)
        {
            _logger.LogError(new SharedBackgroundException(taskName, action, new(exception)));
        }
    }
    private async Task<IReadOnlyCollection<TEntity>> HandleDataAsync(TStep step, string taskName, string action, bool isParallel, CancellationToken cToken)
    {
        try
        {
            IReadOnlyCollection<TEntity> entities;

            _logger.LogTrace(taskName, action, Actions.ProcessableActions.StartHandlingData);

            if (!isParallel)
                entities = await _handler.HandleProcessableStepAsync(step, cToken);
            else
            {
                await _semaphore.WaitAsync();
                entities = await _handler.HandleProcessableStepAsync(step, cToken);
                _semaphore.Release();
            }

            _logger.LogDebug(taskName, action, Actions.ProcessableActions.StopHandlingData);

            return entities;
        }
        catch (Exception exception)
        {
            throw new SharedBackgroundException(taskName, action, new(exception));
        }
    }
}