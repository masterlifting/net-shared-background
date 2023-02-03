using Microsoft.Extensions.DependencyInjection;

using Shared.Background.Core.BackgroundTasks;
using Shared.Background.Core.Base;
using Shared.Persistence.Abstractions.Entities;
using Shared.Persistence.Abstractions.Entities.Catalogs;

namespace Shared.Background.Core.BackgroundTaskServices;

internal sealed class BackgroundTaskServicePulling<TEntity, TStep> : BackgroundTaskServiceBase<TEntity, TStep, BackgroundTaskPulling<TEntity, TStep>>
    where TEntity : class, IPersistentProcess
    where TStep : class, IProcessStep
{
    internal BackgroundTaskServicePulling(IServiceScopeFactory scopeFactory) : base(scopeFactory)
    {
    }
}