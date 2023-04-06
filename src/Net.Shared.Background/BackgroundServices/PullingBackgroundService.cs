using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Net.Shared.Background.BackgroundTasks;
using Net.Shared.Background.Core;
using Net.Shared.Background.Models.Settings;
using Net.Shared.Persistence.Abstractions.Entities;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;

namespace Net.Shared.Background.BackgroundServices;

public abstract class PullingBackgroundService<TTask, TStep, TData> : NetSharedBackgroundService<TStep, TData>
    where TTask : PullingBackgroundTask
    where TStep : class, IPersistentProcessStep
    where TData : class, IPersistentProcess
{
    protected PullingBackgroundService(
        IOptionsMonitor<BackgroundTaskSection> options
        , ILogger logger
        , IServiceScopeFactory scopeFactory) : base(options, logger, new NetSharedBackgroundTaskService<TTask>(scopeFactory))
    {
    }
}