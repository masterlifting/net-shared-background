using Net.Shared.Persistence.Abstractions.Entities;

namespace Net.Shared.Background.Abstractions;

public interface IBackgroundTaskHandler<T> where T : IPersistentProcess
{
    Task Handle(IEnumerable<T> entities, CancellationToken cToken = default);
    Task<IReadOnlyCollection<T>> Handle(CancellationToken cToken = default);
}