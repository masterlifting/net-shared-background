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

namespace Net.Shared.Background.BackgroundTasks;

public abstract class PullingBackgroundTask<TProcess, TProcessStep> : NetSharedBackgroundTask<TProcessStep>
    where TProcess : class, IPersistentProcess
    where TProcessStep : class, IPersistentProcessStep
{
    private readonly SemaphoreSlim _semaphore = new(1);

    private readonly ILogger _logger;
    private readonly IPersistenceRepository<TProcess> _repository;
    private readonly BackgroundTaskHandler<TProcess> _handler;

    protected PullingBackgroundTask(
        ILogger logger
        , IPersistenceRepository<TProcess> processRepository
        , IPersistenceRepository<TProcessStep> catalogRepository
        , BackgroundTaskHandler<TProcess> handler) : base(logger, catalogRepository)
    {
        _logger = logger;
        _repository = processRepository;
        _handler = handler;
    }

    internal override async Task HandleSteps(Queue<TProcessStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
    {
        for (var i = 0; i <= steps.Count; i++)
        {
            var step = steps.Dequeue();

            var entities = await HandleData(taskName, step, settings.Steps.IsParallelProcessing, cToken);

            try
            {
                _logger.LogTrace(StartSavingData(taskName));

                await _repository.Writer.SetProcessableData(null, entities, cToken);

                _logger.LogDebug(StopSavingData(taskName));
            }
            catch (Exception exception)
            {
                _logger.LogError(new NetSharedBackgroundException(exception));
            }
        }
    }
    internal override Task HandleStepsParallel(ConcurrentQueue<TProcessStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
    {
        var tasks = Enumerable.Range(0, steps.Count).Select(_ => HandleStepParallel(taskName, taskCount, steps, settings, cToken));
        return Task.WhenAll(tasks);
    }

    private async Task HandleStepParallel(string taskName, int taskCount, ConcurrentQueue<TProcessStep> steps, BackgroundTaskSettings settings, CancellationToken cToken)
    {
        var isDequeue = steps.TryDequeue(out var step);

        if (steps.Any() && !isDequeue)
            await HandleStepParallel(taskName, taskCount, steps, settings, cToken);

        var entities = await HandleData(taskName, step!, settings.Steps.IsParallelProcessing, cToken);

        try
        {
            _logger.LogTrace(StartSavingData(taskName));

            await _semaphore.WaitAsync(cToken);

            await _repository.Writer.SetProcessableData(null, entities, cToken);

            _semaphore.Release();

            _logger.LogDebug(StopSavingData(taskName));
        }
        catch (Exception exception)
        {
            _logger.LogError(new NetSharedBackgroundException(exception));
        }
    }
    private async Task<IReadOnlyCollection<TProcess>> HandleData(string taskName, TProcessStep step, bool isParallel, CancellationToken cToken)
    {
        try
        {
            IReadOnlyCollection<TProcess> entities;

            _logger.LogTrace(StartHandlingData(taskName));

            if (!isParallel)
            {
                entities = await _handler.HandleStep(step, cToken);
            }
            else
            {
                await _semaphore.WaitAsync(cToken);

                entities = await _handler.HandleStep(step, cToken);

                _semaphore.Release();
            }

            _logger.LogDebug(StopHandlingData(taskName));

            return entities;
        }
        catch (Exception exception)
        {
            throw new NetSharedBackgroundException(exception);
        }
    }
}