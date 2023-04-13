using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using Net.Shared.Background.Abstractions.Core;
using Net.Shared.Background.Handlers;
using Net.Shared.Background.Models;
using Net.Shared.Background.Models.Exceptions;
using Net.Shared.Background.Models.Settings;
using Net.Shared.Persistence.Abstractions.Entities;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;

namespace Net.Shared.Background.Core;

public abstract class NetSharedBackgroundProcessTask : IBackgroundTask
{
    protected NetSharedBackgroundProcessTask(ILogger logger)
    {
        _logger = logger;
        Info = new(string.Empty, 0, new BackgroundTaskSettings());
    }

    #region PRIVATE FIELDS
    private readonly ILogger _logger;
    #endregion

    #region PUBLIC PROPERTIES
    public NetSharedBackgroundTaskInfo Info { get; private set; }
    #endregion

    #region PUBLIC FUNCTIONS
    public async Task Run(NetSharedBackgroundTaskInfo taskInfo, CancellationToken cToken)
    {
        Info = taskInfo;

        var queueSteps = await GetQueueProcessSteps(cToken);

        _logger.LogTrace($"Start task '{taskInfo.Name}' №{taskInfo.Number} with steps count: {queueSteps.Count} as parallel: {taskInfo.Settings.Steps.IsParallelProcessing}");

        if (taskInfo.Settings.Steps.IsParallelProcessing)
            await HandleStepsParallel(new ConcurrentQueue<IPersistentProcessStep>(queueSteps), cToken);
        else
            await HandleSteps(queueSteps, cToken);
    }
    #endregion

    #region PRIVATE FUNCTIONS
    private async Task<Queue<IPersistentProcessStep>> GetQueueProcessSteps(CancellationToken cToken = default)
    {
        var settingSteps = Info.Settings.Steps.Names;

        var steps = await GetPersistentProcessSteps(cToken);

        var result = new Queue<IPersistentProcessStep>(settingSteps.Length);
        var stepNames = steps.ToDictionary(x => x.Name);

        foreach (var stepName in settingSteps)
        {
            if (stepNames.TryGetValue(stepName, out var step))
                result.Enqueue(step);
            else
                throw new NetSharedBackgroundException($"The step '{stepName}' from configuration was not found in the database");
        }

        return result;
    }
    #endregion

    #region ABSTRACT FUNCTIONS
    protected abstract BackgroundProcessStepHandler<T> RegisterProcessStepHandlers<T>() where T : class, IPersistentProcess;

    protected abstract Task<IPersistentProcessStep[]> GetPersistentProcessSteps(CancellationToken cToken = default);
    protected abstract Task HandleSteps(Queue<IPersistentProcessStep> steps, CancellationToken cToken = default);
    protected abstract Task HandleStepsParallel(ConcurrentQueue<IPersistentProcessStep> steps, CancellationToken cToken = default);

    protected abstract Task<T[]> GetProcessableData<T>(IPersistentProcessStep step, int limit, CancellationToken cToken = default) where T : class, IPersistentProcess;
    protected abstract Task<T[]> GetUnprocessableData<T>(IPersistentProcessStep step, int limit, DateTime updateTime, int maxAttempts, CancellationToken cToken = default) where T : class, IPersistentProcess;
    protected abstract Task SetProcessableData<T>(IPersistentProcessStep? step, IEnumerable<T> entities, CancellationToken cToken = default) where T : class, IPersistentProcess;
    #endregion
}