using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Net.Shared.Background.BackgroundTasks;
using Net.Shared.Background.Base;
using Net.Shared.Background.Models.Settings;

namespace Net.Shared.Background.BackgroundServices;

public abstract class ProcessingBackgroundService<T> : NetSharedBackgroundService where T : ProcessingBackgroundTask
{
    protected ProcessingBackgroundService(
        IOptionsMonitor<BackgroundTaskSection> options
        , ILogger logger
        , IServiceScopeFactory scopeFactory) : base(options, logger, new NetSharedBackgroundTaskService<T>(scopeFactory))
    {
    }
}