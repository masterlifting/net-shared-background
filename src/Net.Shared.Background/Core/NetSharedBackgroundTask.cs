using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Net.Shared.Background.Models.Exceptions;
using Net.Shared.Background.Models.Settings;
using Net.Shared.Extensions;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;
using Net.Shared.Persistence.Abstractions.Repositories;

namespace Net.Shared.Background.Base;

public abstract class NetSharedBackgroundTask
{
    private readonly ILogger _logger;
    private readonly IPersistenceRepository<IPersistentProcessStep> _processStepRepository;

    protected NetSharedBackgroundTask(ILogger logger, IPersistenceRepository<IPersistentProcessStep> processStepRepository)
    {
        _logger = logger;
        _processStepRepository = processStepRepository;
    }

    public async Task StartAsync(string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
    {
        var steps = await GetQueueProcessStepsAsync(settings.Steps.Names);

        _logger.LogTrace(taskName, $"Run №{taskCount}", $"Steps: {steps.Count}. As parallel: {settings.Steps.IsParallelProcessing}");

        if (settings.Steps.IsParallelProcessing)
            await ParallelHandleStepsAsync(new ConcurrentQueue<IPersistentProcessStep>(steps), taskName, taskCount, settings, cToken);
        else
            await SuccessivelyHandleStepsAsync(steps, taskName, taskCount, settings, cToken);
    }

    internal async Task<Queue<IPersistentProcessStep>> GetQueueProcessStepsAsync(string[] configurationSteps)
    {
        var result = new Queue<IPersistentProcessStep>(configurationSteps.Length);
        var dbStepNames = await _processStepRepository.Reader.GetCatalogsDictionaryByNameAsync<IPersistentProcessStep>();

        foreach (var configurationStepName in configurationSteps)
        {
            if (dbStepNames.TryGetValue(configurationStepName, out var value))
                result.Enqueue(value);
            else
                throw new NetSharedBackgroundException($"The step '{configurationStepName}' from configuration was not found in the database");
        }

        return result;
    }

    internal abstract Task SuccessivelyHandleStepsAsync(Queue<IPersistentProcessStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken);
    internal abstract Task ParallelHandleStepsAsync(ConcurrentQueue<IPersistentProcessStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken);
}