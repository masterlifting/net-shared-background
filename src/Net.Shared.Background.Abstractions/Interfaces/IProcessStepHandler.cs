using Net.Shared.Persistence.Abstractions.Entities;

namespace Net.Shared.Background.Abstractions.Interfaces;

public interface IProcessStepHandler<T> where T : class, IPersistentProcess
{
    Task HandleStepAsync(IEnumerable<T> entities, CancellationToken cToken);
    Task<IReadOnlyCollection<T>> HandleStepAsync(CancellationToken cToken);
}