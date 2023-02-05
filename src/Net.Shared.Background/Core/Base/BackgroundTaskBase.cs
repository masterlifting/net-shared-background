using Microsoft.Extensions.Logging;

using Shared.Background.Exceptions;
using Shared.Background.Settings;
using Shared.Extensions.Logging;
using Shared.Persistence.Abstractions.Entities.Catalogs;
using Shared.Persistence.Abstractions.Repositories;

using System.Collections.Concurrent;

namespace Shared.Background.Core.Base;

public abstract class BackgroundTaskBase<TStep> where TStep : class, IProcessStep
{
    private readonly ILogger _logger;
    private readonly IPersistenceRepository<TStep> _repository;

    protected BackgroundTaskBase(ILogger logger, IPersistenceRepository<TStep> repository)
    {
        _logger = logger;
        _repository = repository;
    }

    public async Task StartAsync(string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
    {
        var steps = await GetQueueProcessStepsAsync(settings.Steps.Names);

        _logger.LogTrace(taskName, $"Run №{taskCount}", $"Steps: {steps.Count}. As parallel: {settings.Steps.IsParallelProcessing}");

        if (settings.Steps.IsParallelProcessing)
            await ParallelHandleStepsAsync(new ConcurrentQueue<TStep>(steps), taskName, taskCount, settings, cToken);
        else
            await SuccessivelyHandleStepsAsync(steps, taskName, taskCount, settings, cToken);
    }

    internal async Task<Queue<TStep>> GetQueueProcessStepsAsync(string[] configurationSteps)
    {
        var result = new Queue<TStep>(configurationSteps.Length);
        var dbStepNames = await _repository.Reader.GetCatalogsDictionaryByNameAsync<TStep>();

        foreach (var configurationStepName in configurationSteps)
            if (dbStepNames.ContainsKey(configurationStepName))
                result.Enqueue(dbStepNames[configurationStepName]);
            else
                throw new SharedBackgroundException(nameof(BackgroundTaskBase<TStep>), nameof(GetQueueProcessStepsAsync), new($"The step '{configurationStepName}' from configuration was not found in the database"));

        return result;
    }

    internal abstract Task SuccessivelyHandleStepsAsync(Queue<TStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken);
    internal abstract Task ParallelHandleStepsAsync(ConcurrentQueue<TStep> steps, string taskName, int taskCount, BackgroundTaskSettings settings, CancellationToken cToken);
}