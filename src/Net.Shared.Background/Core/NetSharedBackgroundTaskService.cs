using Microsoft.Extensions.DependencyInjection;

using Net.Shared.Background.Abstractions;
using Net.Shared.Background.Models.Settings;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;

namespace Net.Shared.Background.Core;

public sealed class NetSharedBackgroundTaskService<TBackgroundTask, TProcessStep> : IBackgroundTaskService
    where TBackgroundTask : NetSharedBackgroundTask<TProcessStep>
    where TProcessStep : class, IPersistentProcessStep
{
    public string Name { get; }

    private readonly IServiceScopeFactory _scopeFactory;

    public NetSharedBackgroundTaskService(IServiceScopeFactory scopeFactory)
    {
        Name = typeof(TBackgroundTask).Name;
        _scopeFactory = scopeFactory;
    }

    public async Task Run(int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var task = scope.ServiceProvider.GetRequiredService<TBackgroundTask>();

        await task.Start(Name, taskCount, settings, cToken);
    }
}
