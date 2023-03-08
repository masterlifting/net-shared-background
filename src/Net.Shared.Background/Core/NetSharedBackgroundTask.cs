using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Net.Shared.Background.Models.Exceptions;
using Net.Shared.Background.Models.Settings;
using Net.Shared.Persistence.Abstractions.Core.Repositories;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;

namespace Net.Shared.Background.Core;

public abstract class NetSharedBackgroundTask<TProcessStep> where TProcessStep : class, IPersistentProcessStep
{
    private readonly ILogger _logger;
    private readonly IPersistenceRepository<TProcessStep> _processStepRepository;

    protected NetSharedBackgroundTask(
        ILogger logger,
        IPersistenceRepository<TProcessStep> processStepRepository)
    {
        _logger = logger;
        _processStepRepository = processStepRepository;
    }

    internal async Task Start(string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
    {
        var steps = await GetQueueProcessSteps(settings);

        _logger.LogTrace($"Start task '{taskName}' №{taskCount} with steps count: {steps.Count} as parallel: {settings.Steps.IsParallelProcessing}");

        if (settings.Steps.IsParallelProcessing)
            await HandleStepsParallel(new ConcurrentQueue<TProcessStep>(steps), taskName, taskCount, settings, cToken);
        else
            await HandleSteps(steps, taskName, taskCount, settings, cToken);
    }

    private async Task<Queue<TProcessStep>> GetQueueProcessSteps(BackgroundTaskSettings settings)
    {
        var result = new Queue<TProcessStep>(settings.Steps.Names.Length);
        var stepNames = await _processStepRepository.Reader.GetCatalogsDictionaryByName<TProcessStep>();

        foreach (var stepName in settings.Steps.Names)
        {
            if (stepNames.TryGetValue(stepName, out var step))
                result.Enqueue(step);
            else
                throw new NetSharedBackgroundException($"The step '{stepName}' from configuration was not found in the database");
        }

        return result;
    }

    internal abstract Task HandleSteps(Queue<TProcessStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken = default);
    internal abstract Task HandleStepsParallel(ConcurrentQueue<TProcessStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken = default);
}