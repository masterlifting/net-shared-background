using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Net.Shared.Background.Core.Base;
using Net.Shared.Background.Settings.Sections;
using Net.Shared.Persistence.Abstractions.Entities;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;

using Shared.Background.Core.BackgroundTaskServices;

namespace Net.Shared.Background.Core.BackgroundServices;

public abstract class BackgroundServicePulling<TEntity, TStep> : BackgroundServiceBase<TEntity>
    where TEntity : class, IPersistentProcess
    where TStep : class, IProcessStep
{
    protected BackgroundServicePulling(
        IOptionsMonitor<BackgroundTaskSection> options
        , ILogger logger
        , IServiceScopeFactory scopeFactory) : base(options, logger, new BackgroundTaskServicePulling<TEntity, TStep>(scopeFactory))
    {
    }
}