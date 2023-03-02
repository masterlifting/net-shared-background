using Net.Shared.Background.Abstractions;
using Net.Shared.Background.Models.Exceptions;
using Net.Shared.Persistence.Abstractions.Entities;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;

namespace Net.Shared.Background.Handlers;

public sealed class BackgroundTaskHandler<TProcess> where TProcess : class, IPersistentProcess
{
    private readonly Dictionary<int, IBackgroundTaskHandler<TProcess>> _handlers;
    public BackgroundTaskHandler(Dictionary<int, IBackgroundTaskHandler<TProcess>> handlers) => _handlers = handlers;

    public Task HandleStep(IPersistentProcessStep step, IEnumerable<TProcess> data, CancellationToken cToken = default) =>
        _handlers.TryGetValue(step.Id, out var handler)
            ? handler.Handle(data, cToken)
            : throw new NetSharedBackgroundException($"The step: '{step.Name}' is not implemented.");
    public Task<IReadOnlyCollection<TProcess>> HandleStep(IPersistentProcessStep step, CancellationToken cToken = default) =>
        _handlers.TryGetValue(step.Id, out var andler)
            ? andler.Handle(cToken)
            : throw new NetSharedBackgroundException($"The step: '{step.Name}' is not implemented.");
}