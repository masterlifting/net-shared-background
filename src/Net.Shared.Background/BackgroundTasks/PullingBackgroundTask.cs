using Microsoft.Extensions.Logging;

using Net.Shared.Background.Base;
using Net.Shared.Background.Handlers;
using Net.Shared.Background.Models.Exceptions;
using Net.Shared.Background.Models.Settings;
using Net.Shared.Extensions;
using Net.Shared.Persistence.Abstractions.Entities;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;
using Net.Shared.Persistence.Abstractions.Repositories;

using System.Collections.Concurrent;

using static Net.Shared.Background.Models.Constants.BackgroundTaskActions;

namespace Net.Shared.Background.BackgroundTasks;

public abstract class PullingBackgroundTask : NetSharedBackgroundTask
{
    private readonly SemaphoreSlim _semaphore = new(1);

    private readonly ILogger _logger;
    private readonly IPersistenceRepository<IPersistentProcess> _repository;
    private readonly BackgroundTaskHandler<IPersistentProcess> _handler;

    public PullingBackgroundTask(
        ILogger logger
        , IPersistenceRepository<IPersistentProcess> processRepository
        , IPersistenceRepository<IPersistentProcessStep> catalogRepository
        , BackgroundTaskHandler<IPersistentProcess> handler) : base(logger, catalogRepository)
    {
        _logger = logger;
        _repository = processRepository;
        _handler = handler;
    }

    internal override async Task SuccessivelyHandleStepsAsync(Queue<IPersistentProcessStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
    {
        for (var i = 0; i <= steps.Count; i++)
        {
            var step = steps.Dequeue();
            var action = step.Description ?? step.Name;

            var entities = await HandleDataAsync(step, taskName, action, settings.Steps.IsParallelProcessing, cToken);

            try
            {
                _logger.LogTrace(taskName, action, StartSavingData);

                await _repository.Writer.SaveProcessableAsync(null, entities, cToken);

                _logger.LogDebug(taskName, action, StopSavingData);
            }
            catch (Exception exception)
            {
                _logger.LogError(new NetSharedBackgroundException(exception));
            }
        }
    }
    internal override Task ParallelHandleStepsAsync(ConcurrentQueue<IPersistentProcessStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
    {
        var tasks = Enumerable.Range(0, steps.Count).Select(x => ParallelHandleStepAsync(steps, taskName, taskCount, settings, cToken));
        return Task.WhenAll(tasks);
    }

    private async Task ParallelHandleStepAsync(ConcurrentQueue<IPersistentProcessStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
    {
        var isDequeue = steps.TryDequeue(out var step);

        if (steps.Any() && !isDequeue)
            await ParallelHandleStepAsync(steps, taskName, taskCount, settings, cToken);

        var action = step!.Description ?? step.Name;

        var entities = await HandleDataAsync(step, taskName, action, settings.Steps.IsParallelProcessing, cToken);

        try
        {
            _logger.LogTrace(taskName, action, StartSavingData);

            await _semaphore.WaitAsync();
            await _repository.Writer.SaveProcessableAsync(null, entities, cToken);
            _semaphore.Release();

            _logger.LogDebug(taskName, action, StopSavingData);
        }
        catch (Exception exception)
        {
            _logger.LogError(new NetSharedBackgroundException(exception));
        }
    }
    private async Task<IReadOnlyCollection<IPersistentProcess>> HandleDataAsync(IPersistentProcessStep step, string taskName, string action, bool isParallel, CancellationToken cToken)
    {
        try
        {
            IReadOnlyCollection<IPersistentProcess> entities;

            _logger.LogTrace(taskName, action, StartHandlingData);

            if (!isParallel)
                entities = await _handler.HandleStep(step, cToken);
            else
            {
                await _semaphore.WaitAsync();
                entities = await _handler.HandleStep(step, cToken);
                _semaphore.Release();
            }

            _logger.LogDebug(taskName, action, StopHandlingData);

            return entities;
        }
        catch (Exception exception)
        {
            throw new NetSharedBackgroundException(exception);
        }
    }
}