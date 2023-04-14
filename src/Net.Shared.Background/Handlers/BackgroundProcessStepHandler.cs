using Net.Shared.Background.Abstractions.Core;
using Net.Shared.Background.Models.Exceptions;
using Net.Shared.Persistence.Abstractions.Entities;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;

namespace Net.Shared.Background.Handlers;

public sealed class BackgroundProcessStepHandler<T> where T : class, IPersistentProcess
{
    private readonly Dictionary<IPersistentProcessStep, IBackgroundDataHandler<T>> _dataHandlers;
    public BackgroundProcessStepHandler(Dictionary<IPersistentProcessStep, IBackgroundDataHandler<T>> dataHandlers) => _dataHandlers = dataHandlers;

    public Task HandleStep(IPersistentProcessStep step, IEnumerable<T> data, CancellationToken cToken = default) =>
        _dataHandlers.TryGetValue(step, out var handler)
            ? handler.Handle(data, cToken)
            : throw new NetSharedBackgroundException($"The step: '{step.Name}' is not implemented.");
    public Task<IReadOnlyCollection<T>> HandleStep(IPersistentProcessStep step, CancellationToken cToken = default) =>
        _dataHandlers.TryGetValue(step, out var andler)
            ? andler.Handle(cToken)
            : throw new NetSharedBackgroundException($"The step: '{step.Name}' is not implemented.");
}