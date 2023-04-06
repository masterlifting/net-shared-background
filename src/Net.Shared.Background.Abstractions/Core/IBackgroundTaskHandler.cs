using Net.Shared.Persistence.Abstractions.Entities;

namespace Net.Shared.Background.Abstractions.Core;

public interface IBackgroundTaskHandler
{
    Task Handle<TData>(IEnumerable<TData> entities, CancellationToken cToken = default) where TData : class, IPersistentProcess;
    Task<IReadOnlyCollection<TData>> Handle<TData>(CancellationToken cToken = default) where TData : class, IPersistentProcess;
}