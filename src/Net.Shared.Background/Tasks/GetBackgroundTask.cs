using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using Net.Shared.Background.Core;
using Net.Shared.Background.Models.Exceptions;
using Net.Shared.Persistence.Abstractions.Entities;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;

using static Net.Shared.Background.Models.Constants.BackgroundTaskActions;

namespace Net.Shared.Background.Tasks;

public abstract class GetBackgroundTask<T> : BackgroundTask<T> where T : class, IPersistentProcess
{
    protected GetBackgroundTask(ILogger logger) : base(logger) => _logger = logger;

    #region PRIVATE FIELDS
    private readonly SemaphoreSlim _semaphore = new(1);
    private readonly ILogger _logger;
    #endregion

    #region OVERRIDED FUNCTIONS
    protected override async Task HandleSteps(Queue<IPersistentProcessStep> steps, BackgroundStepHandler<T> handler, CancellationToken cToken)
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
                _logger.LogTrace(StartHandlingData(TaskInfo.Name, currentStep.Name));

                var data = await handler.Get(currentStep, cToken);

                _logger.LogDebug(StopHandlingData(TaskInfo.Name, currentStep.Name));

                steps.TryPeek(out var nextStep);

                await SaveResult(currentStep, nextStep, data, cToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(new NetSharedBackgroundException(exception));
            }
        }
    }
    protected override Task HandleStepsParallel(ConcurrentQueue<IPersistentProcessStep> steps, BackgroundStepHandler<T> handler, CancellationToken cToken)
    {
        var tasks = Enumerable.Range(0, steps.Count).Select(async _ =>
        {
            var isDequeue = steps.TryDequeue(out var currentStep);

            if (!isDequeue)
            {
                _logger.LogWarn($"No steps to process.");
                return;
            }

            try
            {
                await _semaphore.WaitAsync(cToken);

                _logger.LogTrace(StartHandlingData(TaskInfo.Name, currentStep!.Name));

                var data = await handler.Get(currentStep, cToken);

                _logger.LogDebug(StopHandlingData(TaskInfo.Name, currentStep!.Name));

                steps.TryPeek(out var nextStep);

                await SaveResult(currentStep, nextStep, data, cToken);
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