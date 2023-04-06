using Net.Shared.Background.Abstractions.Core;
using Net.Shared.Background.Models.Exceptions;
using Net.Shared.Persistence.Abstractions.Entities;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;

namespace Net.Shared.Background.Handlers;

public sealed class BackgroundProcessTaskHandler<T> where T : class, IPersistentProcess
{
    private readonly Dictionary<int, IBackgroundProcessTaskHandler<T>> _handlers;
    public BackgroundProcessTaskHandler(Dictionary<int, IBackgroundProcessTaskHandler<T>> handlers) => _handlers = handlers;

    public Task HandleStep(IPersistentProcessStep step, IEnumerable<T> data, CancellationToken cToken = default) =>
        _handlers.TryGetValue(step.Id, out var handler)
            ? handler.Handle(data, cToken)
            : throw new NetSharedBackgroundException($"The step: '{step.Name}' is not implemented.");
    public Task<IReadOnlyCollection<T>> HandleStep(IPersistentProcessStep step, CancellationToken cToken = default) =>
        _handlers.TryGetValue(step.Id, out var andler)
            ? andler.Handle(cToken)
            : throw new NetSharedBackgroundException($"The step: '{step.Name}' is not implemented.");
}