using Microsoft.Extensions.DependencyInjection;
using Net.Shared.Background.Abstractions;
using Net.Shared.Background.Models.Settings;

namespace Net.Shared.Background.Base;

public sealed class NetSharedBackgroundTaskService<T> : IBackgroundTaskService where T : NetSharedBackgroundTask
{
    public string TaskName { get; }

    private readonly IServiceScopeFactory _scopeFactory;

    public NetSharedBackgroundTaskService(IServiceScopeFactory scopeFactory)
    {
        TaskName = typeof(T).Name;
        _scopeFactory = scopeFactory;
    }

    public async Task RunTaskAsync(int taskCount, BackgroundTaskSettings settings, CancellationToken cToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var task = scope.ServiceProvider.GetRequiredService<T>();

        await task.StartAsync(TaskName, taskCount, settings, cToken);
    }
}
