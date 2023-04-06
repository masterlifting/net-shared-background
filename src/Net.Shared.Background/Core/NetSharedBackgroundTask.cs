using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Net.Shared.Background.Models.Exceptions;
using Net.Shared.Background.Models.Settings;
using Net.Shared.Persistence.Abstractions.Entities;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;
using Net.Shared.Persistence.Abstractions.Repositories;

namespace Net.Shared.Background.Core;

public abstract class NetSharedBackgroundTask
{
    private readonly ILogger _logger;
    private readonly IPersistenceProcessRepository _repository;

    protected NetSharedBackgroundTask(
        ILogger logger,
        IPersistenceProcessRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    internal async Task Start<TStep, TData>(string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
        where TStep : class, IPersistentProcessStep
        where TData : class, IPersistentProcess
    {
        var steps = await GetQueueProcessSteps<TStep>(settings, cToken);

        _logger.LogTrace($"Start task '{taskName}' №{taskCount} with steps count: {steps.Count} as parallel: {settings.Steps.IsParallelProcessing}");

        if (settings.Steps.IsParallelProcessing)
            await HandleStepsParallel<TStep, TData>(new ConcurrentQueue<TStep>(steps), taskName, taskCount, settings, cToken);
        else
            await HandleSteps<TStep, TData>(steps, taskName, taskCount, settings, cToken);
    }

    private async Task<Queue<TStep>> GetQueueProcessSteps<TStep>(BackgroundTaskSettings settings, CancellationToken cToken = default)
        where TStep : class, IPersistentProcessStep
    {
        var result = new Queue<TStep>(settings.Steps.Names.Length);
        var stepNames = await _repository.GetSteps<TStep>(cToken);

        foreach (var stepName in settings.Steps.Names)
        {
            if (stepNames.TryGetValue(stepName, out var step))
                result.Enqueue(step);
            else
                throw new NetSharedBackgroundException($"The step '{stepName}' from configuration was not found in the database");
        }

        return result;
    }

    internal abstract Task HandleSteps<TStep, TData>(Queue<TStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken = default)
        where TStep : class, IPersistentProcessStep
        where TData : class, IPersistentProcess;
    internal abstract Task HandleStepsParallel<TStep, TData>(ConcurrentQueue<TStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken = default)
        where TStep : class, IPersistentProcessStep
        where TData : class, IPersistentProcess;
}