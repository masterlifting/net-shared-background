using Net.Shared.Models.Domain;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;

namespace Net.Shared.Background.Abstractions;

public interface IBackgroundTaskHandler<T> where T : class
{
    Task<Result<T>> Handle(IPersistentProcessStep step, IEnumerable<T> data, CancellationToken cToken);
}