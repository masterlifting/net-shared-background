using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Shared.Background.Core.BackgroundTaskServices;
using Shared.Background.Core.Base;
using Shared.Background.Settings.Sections;
using Shared.Persistence.Abstractions.Entities;
using Shared.Persistence.Abstractions.Entities.Catalogs;

namespace Shared.Background.Core.BackgroundServices;

public abstract class BackgroundServiceProcessing<TEntity, TStep> : BackgroundServiceBase<TEntity>
    where TEntity : class, IPersistentProcess
    where TStep : class, IProcessStep
{
    protected BackgroundServiceProcessing(
        IOptionsMonitor<BackgroundTaskSection> options
        , ILogger logger
        , IServiceScopeFactory scopeFactory) : base(options, logger, new BackgroundTaskServiceProcessing<TEntity, TStep>(scopeFactory))
    {
    }
}