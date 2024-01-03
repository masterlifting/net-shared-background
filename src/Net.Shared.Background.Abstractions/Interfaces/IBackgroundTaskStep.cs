using Net.Shared.Abstractions.Models.Data;
using Net.Shared.Persistence.Abstractions.Interfaces.Entities.Catalogs;

namespace Net.Shared.Background.Abstractions.Interfaces;

public interface IBackgroundTaskStep<T> where T : class
{
    Task<Result<T>> Handle(IPersistentProcessStep step, IEnumerable<T> data, CancellationToken cToken);
}
