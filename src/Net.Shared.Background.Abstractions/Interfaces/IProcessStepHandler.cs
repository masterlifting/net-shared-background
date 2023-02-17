using Net.Shared.Persistence.Abstractions.Entities;

namespace Net.Shared.Background.Abstractions.Interfaces;

public interface IProcessStepHandler
{
    Task HandleStepAsync(IEnumerable<IPersistentProcess> entities, CancellationToken cToken);
    Task<IReadOnlyCollection<IPersistentProcess>> HandleStepAsync(CancellationToken cToken);
}