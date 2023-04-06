using Microsoft.Extensions.DependencyInjection;
using Net.Shared.Background.Abstractions.Core;
using Net.Shared.Background.Models.Settings;
using Net.Shared.Persistence.Abstractions.Entities;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;

namespace Net.Shared.Background.Core;

public sealed class NetSharedBackgroundTaskService<TTask> : IBackgroundTaskService
    where TTask : NetSharedBackgroundTask
{
    public string TaskName { get; }

    private readonly IServiceScopeFactory _scopeFactory;

    public NetSharedBackgroundTaskService(IServiceScopeFactory scopeFactory)
    {
        TaskName = typeof(TTask).Name;
        _scopeFactory = scopeFactory;
    }

    public async Task StartTask<TStep, TData>(int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
        where TStep : class, IPersistentProcessStep
        where TData : class, IPersistentProcess
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var task = scope.ServiceProvider.GetRequiredService<TTask>();

        await task.Start<TStep, TData>(TaskName, taskCount, settings, cToken);
    }
}
