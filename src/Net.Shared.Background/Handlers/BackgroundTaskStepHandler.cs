using Net.Shared.Background.Abstractions.Interfaces;
using Net.Shared.Background.Exceptions;
using Net.Shared.Persistence.Abstractions.Entities;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;

namespace Net.Shared.Background.Handlers;

public sealed class BackgroundTaskStepHandler
{
    private readonly Dictionary<int, IProcessStepHandler> _handlers;
    public BackgroundTaskStepHandler(Dictionary<int, IProcessStepHandler> handlers) => _handlers = handlers;

    public Task HandleProcessableStepAsync(IProcessStep step, IEnumerable<IPersistentProcess> data, CancellationToken cToken) => _handlers.ContainsKey(step.Id)
        ? _handlers[step.Id].HandleStepAsync(data, cToken)
        : throw new NetSharedBackgroundException($"The step: '{step.Name}' is not implemented");
    public Task<IReadOnlyCollection<IPersistentProcess>> HandleProcessableStepAsync(IProcessStep step, CancellationToken cToken) => _handlers.ContainsKey(step.Id)
        ? _handlers[step.Id].HandleStepAsync(cToken)
        : throw new NetSharedBackgroundException($"The step: '{step.Name}' is not implemented");
}