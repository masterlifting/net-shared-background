using Shared.Persistence.Abstractions.Entities;

namespace Shared.Background.Interfaces;

public interface IProcessStepHandler<T> where T : class, IPersistentProcess
{
    Task HandleStepAsync(IEnumerable<T> entities, CancellationToken cToken);
    Task<IReadOnlyCollection<T>> HandleStepAsync(CancellationToken cToken);
}