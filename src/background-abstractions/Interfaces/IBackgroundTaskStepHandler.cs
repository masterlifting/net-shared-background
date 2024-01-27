using Net.Shared.Persistence.Abstractions.Interfaces.Entities.Catalogs;

namespace Net.Shared.Background.Abstractions.Interfaces;

public interface IBackgroundTaskStepHandler<T> where T : class
{
    Task Handle(string taskName, IServiceProvider serviceProvider, IPersistentProcessStep step, IEnumerable<T> data, CancellationToken cToken);
}
