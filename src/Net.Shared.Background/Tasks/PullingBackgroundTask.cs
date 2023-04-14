using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using Net.Shared.Background.Core;
using Net.Shared.Background.Handlers;
using Net.Shared.Background.Models.Exceptions;
using Net.Shared.Persistence.Abstractions.Entities;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;

using static Net.Shared.Background.Models.Constants.BackgroundTaskActions;

namespace Net.Shared.Background.Tasks;

public abstract class PullingBackgroundTask<T> : NetSharedBackgroundProcessTask<T> where T : class, IPersistentProcess
{
    protected PullingBackgroundTask(ILogger logger) : base(logger) => _logger = logger;

    #region PRIVATE FIELDS
    private readonly SemaphoreSlim _semaphore = new(1);
    private readonly ILogger _logger;
    #endregion

    #region OVERRIDED FUNCTIONS
    protected override async Task HandleSteps(Queue<IPersistentProcessStep> steps, BackgroundProcessStepHandler<T> stepsHandler, CancellationToken cToken)
    {
        for (var i = 0; i <= steps.Count; i++)
        {
            var step = steps.Dequeue();

            try
            {
                _logger.LogTrace(StartHandlingData(Info.Name, step.Name));

                var entities = await stepsHandler.HandleStep(step, cToken);

                _logger.LogDebug(StopHandlingData(Info.Name, step.Name));

                _logger.LogTrace(StartSavingData(Info.Name, step.Name));

                await SetProcessableData(null, entities, cToken);

                _logger.LogDebug(StopSavingData(Info.Name, step.Name));
            }
            catch (Exception exception)
            {
                _logger.LogError(new NetSharedBackgroundException(exception));
            }
        }
    }
    protected override Task HandleStepsParallel(ConcurrentQueue<IPersistentProcessStep> steps, BackgroundProcessStepHandler<T> stepsHandler, CancellationToken cToken)
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

                _logger.LogTrace(StartHandlingData(Info.Name, step!.Name));

                var entities = await stepsHandler.HandleStep(step, cToken);

                _logger.LogDebug(StopHandlingData(Info.Name, step!.Name));

                _logger.LogTrace(StartSavingData(Info.Name, step!.Name));

                await SetProcessableData(null, entities, cToken);

                _logger.LogDebug(StopSavingData(Info.Name, step!.Name));
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
    #endregion
}