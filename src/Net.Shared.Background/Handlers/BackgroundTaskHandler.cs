using Net.Shared.Background.Abstractions;
using Net.Shared.Background.Models.Exceptions;
using Net.Shared.Persistence.Abstractions.Entities;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;

namespace Net.Shared.Background.Handlers;

public sealed class BackgroundTaskHandler<T> where T : IPersistentProcess
{
    private readonly Dictionary<IPersistentProcessStep, IBackgroundTaskHandler<T>> _handlers;
    public BackgroundTaskHandler(Dictionary<IPersistentProcessStep, IBackgroundTaskHandler<T>> handlers) => _handlers = handlers;

    public Task HandleStep(IPersistentProcessStep step, IEnumerable<T> data, CancellationToken cToken = default) => _handlers.TryGetValue(step, out var handler) 
        ? handler.Handle(data, cToken)
        : throw new NetSharedBackgroundException($"The step: '{step.Name}' is not implemented.");
    public Task<IReadOnlyCollection<T>> HandleStep(IPersistentProcessStep step, CancellationToken cToken = default)  => _handlers.TryGetValue(step, out var andler) 
        ? andler.Handle(cToken)
        : throw new NetSharedBackgroundException($"The step: '{step.Name}' is not implemented.");
}