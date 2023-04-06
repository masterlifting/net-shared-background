using Net.Shared.Persistence.Abstractions.Entities;

namespace Net.Shared.Background.Abstractions.Core;

public interface IBackgroundProcessTaskHandler<T> where T : class, IPersistentProcess
{
    Task Handle(IEnumerable<T> entities, CancellationToken cToken = default);
    Task<IReadOnlyCollection<T>> Handle(CancellationToken cToken = default);
}