using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using Net.Shared.Background.Abstractions.Core;
using Net.Shared.Background.Models;
using Net.Shared.Background.Models.Exceptions;
using Net.Shared.Background.Models.Settings;
using Net.Shared.Persistence.Abstractions.Entities;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;

using static Net.Shared.Persistence.Models.Constants.Enums;
using static Net.Shared.Background.Models.Constants.BackgroundTaskActions;

namespace Net.Shared.Background.Core;

public abstract class BackgroundTask<T> : IBackgroundTask where T : class, IPersistentProcess
{
    protected BackgroundTask(ILogger logger)
    {
        _logger = logger;
        TaskInfo = new(string.Empty, 0, new BackgroundTaskSettings());
    }

    #region PRIVATE FIELDS
    private readonly ILogger _logger;
    #endregion

    #region PUBLIC PROPERTIES
    public NetSharedBackgroundTaskInfo TaskInfo { get; private set; }
    #endregion

    #region PUBLIC FUNCTIONS
    public async Task Run(NetSharedBackgroundTaskInfo taskInfo, CancellationToken cToken)
    {
        TaskInfo = taskInfo;

        var steps = await GetQueueSteps(cToken);

        var handler = RegisterStepHandler(steps);

        _logger.LogTrace($"Start task '{taskInfo.Name}' №{taskInfo.Number} with steps count: {steps.Count} as parallel: {taskInfo.Settings.Steps.IsParallelProcessing}.");

        if (taskInfo.Settings.Steps.IsParallelProcessing)
            await HandleStepsParallel(new ConcurrentQueue<IPersistentProcessStep>(steps), handler, cToken);
        else
            await HandleSteps(steps, handler, cToken);
    }
    protected async Task SaveResult(IPersistentProcessStep currentStep, IPersistentProcessStep? nextStep, IEnumerable<T> data, CancellationToken cToken)
    {
        _logger.LogTrace(StartSavingData(TaskInfo.Name, currentStep.Name));

        var stopMessage = StopSavingData(TaskInfo.Name, currentStep.Name);

        if (nextStep is not null)
        {
            foreach (var item in data.Where(x => x.ProcessStatusId == (int)ProcessStatuses.Processed))
                item.ProcessStatusId = (int)ProcessStatuses.Ready;

            stopMessage += $" Next step: '{nextStep.Name}'";
        }

        await SaveData(nextStep, data, cToken);

        _logger.LogDebug(stopMessage);
    }
    #endregion

    #region PRIVATE FUNCTIONS
    private async Task<Queue<IPersistentProcessStep>> GetQueueSteps(CancellationToken cToken = default)
    {
        var settingSteps = TaskInfo.Settings.Steps.Names;

        var steps = await GetSteps(cToken);

        var result = new Queue<IPersistentProcessStep>(settingSteps.Length);

        var stepNames = steps.ToDictionary(x => x.Name);

        foreach (var stepName in settingSteps)
        {
            if (stepNames.TryGetValue(stepName, out var step))
                result.Enqueue(step);
            else
                throw new NetSharedBackgroundException($"The handler '{stepName}' from _options was not found in the database");
        }

        return result;
    }
    #endregion

    #region ABSTRACT FUNCTIONS

    protected abstract Task<IPersistentProcessStep[]> GetSteps(CancellationToken cToken = default);
    protected abstract BackgroundStepHandler<T> RegisterStepHandler(IReadOnlyCollection<IPersistentProcessStep> steps);

    protected abstract Task HandleSteps(Queue<IPersistentProcessStep> steps, BackgroundStepHandler<T> handler, CancellationToken cToken = default);
    protected abstract Task HandleStepsParallel(ConcurrentQueue<IPersistentProcessStep> steps, BackgroundStepHandler<T> handler, CancellationToken cToken = default);

    protected abstract Task<T[]> GetProcessableData(IPersistentProcessStep step, int limit, CancellationToken cToken = default);
    protected abstract Task<T[]> GetUnprocessableData(IPersistentProcessStep step, int limit, DateTime updateTime, int maxAttempts, CancellationToken cToken = default);
    protected abstract Task SaveData(IPersistentProcessStep? step, IEnumerable<T> entities, CancellationToken cToken = default);
    #endregion
}