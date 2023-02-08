using Microsoft.Extensions.DependencyInjection;

using Net.Shared.Background.Core.BackgroundTasks;
using Net.Shared.Background.Core.Base;
using Net.Shared.Persistence.Abstractions.Entities;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;

namespace Net.Shared.Background.Core.BackgroundTaskServices;

internal sealed class BackgroundTaskServicePulling<TEntity, TStep> : BackgroundTaskServiceBase<TEntity, TStep, BackgroundTaskPulling<TEntity, TStep>>
    where TEntity : class, IPersistentProcess
    where TStep : class, IProcessStep
{
    internal BackgroundTaskServicePulling(IServiceScopeFactory scopeFactory) : base(scopeFactory)
    {
    }
}