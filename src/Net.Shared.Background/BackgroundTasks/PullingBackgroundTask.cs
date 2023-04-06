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

public abstract class PullingBackgroundTask<TData> : NetSharedBackgroundProcessTask
    where TData : class, IPersistentProcess
{
    private readonly SemaphoreSlim _semaphore = new(1);

    private readonly ILogger _logger;
    private readonly BackgroundProcessTaskHandler<TData> _handler;
    private readonly IPersistenceProcessRepository _repository;

    protected PullingBackgroundTask(
        ILogger logger
        , IPersistenceProcessRepository repository
        , BackgroundProcessTaskHandler<TData> handler) : base(logger)
    {
        _logger = logger;
        _handler = handler;
        _repository = repository;
    }

    internal override async Task HandleSteps(Queue<IPersistentProcessStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
    {
        for (var i = 0; i <= steps.Count; i++)
        {
            var step = steps.Dequeue();

            try
            {
                _logger.LogTrace(StartHandlingData(taskName, step.Name));

                var entities = await _handler.HandleStep(step, cToken);

                _logger.LogDebug(StopHandlingData(taskName, step.Name));

                _logger.LogTrace(StartSavingData(taskName, step.Name));

                await _repository.SetProcessableData(null, entities, cToken);

                _logger.LogDebug(StopSavingData(taskName, step.Name));
            }
            catch (Exception exception)
            {
                _logger.LogError(new NetSharedBackgroundException(exception));
            }
        }
    }
    internal override Task HandleStepsParallel(ConcurrentQueue<IPersistentProcessStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
    {
        var tasks = Enumerable.Range(0, steps.Count).Select(async _ =>
        {
            var isDequeue = steps.TryDequeue(out var step);

            if (!isDequeue)
            {
                _logger.LogWarn($"No steps to process by step {step?.Name}.");
                return;
            }

            try
            {
                await _semaphore.WaitAsync(cToken);

                _logger.LogTrace(StartHandlingData(taskName, step!.Name));

                var entities = await _handler.HandleStep(step, cToken);

                _logger.LogDebug(StopHandlingData(taskName, step!.Name));

                _logger.LogTrace(StartSavingData(taskName, step!.Name));

                await _repository.SetProcessableData(null, entities, cToken);

                _logger.LogDebug(StopSavingData(taskName, step!.Name));
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
                var exception = task.Exception ?? new AggregateException($"Unhandled exception of the paralel task.");
                _logger.LogError(new NetSharedBackgroundException(exception));
            }
        }, cToken);
    }
}