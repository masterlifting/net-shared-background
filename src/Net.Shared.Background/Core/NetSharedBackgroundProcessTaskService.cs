using Microsoft.Extensions.DependencyInjection;
using Net.Shared.Background.Abstractions.Core;
using Net.Shared.Background.Models.Settings;
using Net.Shared.Persistence.Abstractions.Entities;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;
using Net.Shared.Persistence.Abstractions.Repositories;

namespace Net.Shared.Background.Core;

public sealed class NetSharedBackgroundProcessTaskService<TTask, TStep, TData> : IBackgroundTaskService
    where TTask : NetSharedBackgroundProcessTask
    where TStep : class, IPersistentProcessStep
    where TData : class, IPersistentProcess
{
    public string TaskName { get; }

    private readonly IServiceScopeFactory _scopeFactory;

    public NetSharedBackgroundProcessTaskService(IServiceScopeFactory scopeFactory)
    {
        TaskName = typeof(TTask).Name;
        _scopeFactory = scopeFactory;
    }

    public async Task StartTask(int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var task = scope.ServiceProvider.GetRequiredService<TTask>();
        var persistenceRepository = scope.ServiceProvider.GetRequiredService<IPersistenceProcessRepository> ();
        var steps = await persistenceRepository.GetSteps<TStep>(cToken);

        await task.Start(TaskName, taskCount, steps, settings, cToken);
    }
}
