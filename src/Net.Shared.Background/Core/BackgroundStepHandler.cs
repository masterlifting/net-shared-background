using Net.Shared.Background.Abstractions.Core;
using Net.Shared.Background.Models.Exceptions;
using Net.Shared.Persistence.Abstractions.Entities;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;

namespace Net.Shared.Background.Core;

public sealed class BackgroundStepHandler<T> where T : class, IPersistentProcess
{
    private readonly Dictionary<IPersistentProcessStep, IBackgroundDataHandler<T>> _handlers;
    public BackgroundStepHandler(Dictionary<IPersistentProcessStep, IBackgroundDataHandler<T>> handlers) => _handlers = handlers;

    public Task Post(IPersistentProcessStep step, IEnumerable<T> data, CancellationToken cToken = default) =>
        _handlers.TryGetValue(step, out var handler)
            ? handler.Post(data, cToken)
            : throw new NetSharedBackgroundException($"The step: '{step.Name}' is not implemented.");
    public Task<IReadOnlyCollection<T>> Get(IPersistentProcessStep step, CancellationToken cToken = default) =>
        _handlers.TryGetValue(step, out var handler)
            ? handler.Get(cToken)
            : throw new NetSharedBackgroundException($"The step: '{step.Name}' is not implemented.");
}