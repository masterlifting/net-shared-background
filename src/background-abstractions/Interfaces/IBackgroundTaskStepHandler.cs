using Net.Shared.Abstractions.Models.Data;
using Net.Shared.Persistence.Abstractions.Interfaces.Entities.Catalogs;

namespace Net.Shared.Background.Abstractions.Interfaces;

public interface IBackgroundTaskStepHandler<T> where T : class
{
    Task<Result<T>> Handle(string taskName, IPersistentProcessStep step, IEnumerable<T> data, CancellationToken cToken);
}
