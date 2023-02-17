using Microsoft.Extensions.Logging;

using Net.Shared.Background.Exceptions;
using Net.Shared.Background.Models.Settings;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;
using Net.Shared.Persistence.Abstractions.Repositories;

using System.Collections.Concurrent;

namespace Net.Shared.Background.Base;

public abstract class NetSharedBackgroundTask
{
    private readonly ILogger _logger;
    private readonly IPersistenceRepository<IProcessStep> _repository;

    protected NetSharedBackgroundTask(ILogger logger, IPersistenceRepository<IProcessStep> repository)
    {
        _logger = logger;
        _repository = repository;
    }

    public async Task StartAsync(string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
    {
        var steps = await GetQueueProcessStepsAsync(settings.Steps.Names);

        _logger.LogTrace(taskName, $"Run №{taskCount}", $"Steps: {steps.Count}. As parallel: {settings.Steps.IsParallelProcessing}");

        if (settings.Steps.IsParallelProcessing)
            await ParallelHandleStepsAsync(new ConcurrentQueue<IProcessStep>(steps), taskName, taskCount, settings, cToken);
        else
            await SuccessivelyHandleStepsAsync(steps, taskName, taskCount, settings, cToken);
    }

    internal async Task<Queue<IProcessStep>> GetQueueProcessStepsAsync(string[] configurationSteps)
    {
        var result = new Queue<IProcessStep>(configurationSteps.Length);
        var dbStepNames = await _repository.Reader.GetCatalogsDictionaryByNameAsync<IProcessStep>();

        foreach (var configurationStepName in configurationSteps)
            if (dbStepNames.ContainsKey(configurationStepName))
                result.Enqueue(dbStepNames[configurationStepName]);
            else
                throw new NetSharedBackgroundException($"The step '{configurationStepName}' from configuration was not found in the database");

        return result;
    }

    internal abstract Task SuccessivelyHandleStepsAsync(Queue<IProcessStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken);
    internal abstract Task ParallelHandleStepsAsync(ConcurrentQueue<IProcessStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken);
}