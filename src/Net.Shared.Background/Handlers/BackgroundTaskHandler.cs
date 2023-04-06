using Net.Shared.Background.Abstractions.Core;
using Net.Shared.Background.Models.Exceptions;
using Net.Shared.Persistence.Abstractions.Entities;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;

namespace Net.Shared.Background.Handlers;

public sealed class BackgroundTaskHandler
{
    private readonly Dictionary<int, IBackgroundTaskHandler> _handlers;
    public BackgroundTaskHandler(Dictionary<int, IBackgroundTaskHandler> handlers) => _handlers = handlers;

    public Task HandleStep<TData>(IPersistentProcessStep step, IEnumerable<TData> data, CancellationToken cToken = default)
        where TData : class, IPersistentProcess =>
        _handlers.TryGetValue(step.Id, out var handler)
            ? handler.Handle(data, cToken)
            : throw new NetSharedBackgroundException($"The step: '{step.Name}' is not implemented.");
    public Task<IReadOnlyCollection<TData>> HandleStep<TData>(IPersistentProcessStep step, CancellationToken cToken = default)
        where TData : class, IPersistentProcess =>
        _handlers.TryGetValue(step.Id, out var andler)
            ? andler.Handle<TData>(cToken)
            : throw new NetSharedBackgroundException($"The step: '{step.Name}' is not implemented.");
}