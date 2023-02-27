using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Net.Shared.Background.BackgroundTasks;
using Net.Shared.Background.Core;
using Net.Shared.Background.Models.Settings;
using Net.Shared.Persistence.Abstractions.Entities;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;

namespace Net.Shared.Background.BackgroundServices;

public abstract class PullingBackgroundService<TBackgroundTask, TProcess, TProcessStep> : NetSharedBackgroundService 
    where TBackgroundTask : PullingBackgroundTask<TProcess, TProcessStep>
    where TProcess : class, IPersistentProcess
    where TProcessStep : class, IPersistentProcessStep
{
    protected PullingBackgroundService(
        IOptionsMonitor<BackgroundTaskSection> options
        , ILogger logger
        , IServiceScopeFactory scopeFactory) : base(options, logger, new NetSharedBackgroundTaskService<TBackgroundTask, TProcessStep>(scopeFactory))
    {
    }
}