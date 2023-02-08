using Microsoft.Extensions.DependencyInjection;

using Net.Shared.Background.Abstractions.Interfaces;

using Net.Shared.Background.Settings;
using Net.Shared.Persistence.Abstractions.Entities;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;

namespace Net.Shared.Background.Core.Base;

public abstract class BackgroundTaskServiceBase<TEntity, TStep, TTask> : IBackgroundTaskService
    where TEntity : class, IPersistentProcess
    where TStep : class, IProcessStep
    where TTask : BackgroundTaskBase<TStep>
{
    public string TaskName { get; }

    private readonly IServiceScopeFactory _scopeFactory;

    protected BackgroundTaskServiceBase(IServiceScopeFactory scopeFactory)
    {
        TaskName = typeof(TEntity).Name + '.' + typeof(TTask).Name[..^2];
        _scopeFactory = scopeFactory;
    }

    public async Task RunTaskAsync(int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var task = scope.ServiceProvider.GetRequiredService<TTask>();

        await task.StartAsync(TaskName, taskCount, settings, cToken);
    }
}
