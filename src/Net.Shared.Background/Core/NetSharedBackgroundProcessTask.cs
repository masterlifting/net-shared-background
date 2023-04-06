using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using Net.Shared.Background.Models.Exceptions;
using Net.Shared.Background.Models.Settings;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;

namespace Net.Shared.Background.Core;

public abstract class NetSharedBackgroundProcessTask
{
    private readonly ILogger _logger;
    protected NetSharedBackgroundProcessTask(ILogger logger) =>  _logger = logger; 

    internal async Task Start(string taskName, int taskCount, IEnumerable<IPersistentProcessStep> steps,  BackgroundTaskSettings settings, CancellationToken cToken)
    {
        var queueSteps = GetQueueProcessSteps(steps, settings, cToken);

        _logger.LogTrace($"Start task '{taskName}' №{taskCount} with steps count: {queueSteps.Count} as parallel: {settings.Steps.IsParallelProcessing}");

        if (settings.Steps.IsParallelProcessing)
            await HandleStepsParallel(new ConcurrentQueue<IPersistentProcessStep>(queueSteps), taskName, taskCount, settings, cToken);
        else
            await HandleSteps(queueSteps, taskName, taskCount, settings, cToken);
    }

    internal abstract Task HandleSteps(Queue<IPersistentProcessStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken = default);
    internal abstract Task HandleStepsParallel(ConcurrentQueue<IPersistentProcessStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken = default);

    private static Queue<IPersistentProcessStep> GetQueueProcessSteps(IEnumerable<IPersistentProcessStep> steps, BackgroundTaskSettings settings, CancellationToken cToken = default)
    {
        var result = new Queue<IPersistentProcessStep>(settings.Steps.Names.Length);
        var stepNames = steps.ToDictionary(x => x.Name);

        foreach (var stepName in settings.Steps.Names)
        {
            if (stepNames.TryGetValue(stepName, out var step))
                result.Enqueue(step);
            else
                throw new NetSharedBackgroundException($"The step '{stepName}' from configuration was not found in the database");
        }

        return result;
    }
}