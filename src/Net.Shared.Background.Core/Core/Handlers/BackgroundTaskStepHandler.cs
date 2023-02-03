using Shared.Background.Exceptions;
using Shared.Background.Interfaces;
using Shared.Persistence.Abstractions.Entities;
using Shared.Persistence.Abstractions.Entities.Catalogs;

namespace Shared.Background.Core.Handlers;

public sealed class BackgroundTaskStepHandler<T> where T : class, IPersistentProcess
{
    private readonly Dictionary<int, IProcessStepHandler<T>> _handlers;
    public BackgroundTaskStepHandler(Dictionary<int, IProcessStepHandler<T>> handlers) => _handlers = handlers;

    public Task HandleProcessableStepAsync(IProcessStep step, IEnumerable<T> data, CancellationToken cToken) => _handlers.ContainsKey(step.Id)
        ? _handlers[step.Id].HandleStepAsync(data, cToken)
        : throw new SharedBackgroundException(typeof(T).Name + "Handler", nameof(HandleProcessableStepAsync), new($"Step: '{step.Name}' is not implemented'"));
    public Task<IReadOnlyCollection<T>> HandleProcessableStepAsync(IProcessStep step, CancellationToken cToken) => _handlers.ContainsKey(step.Id)
        ? _handlers[step.Id].HandleStepAsync(cToken)
        : throw new SharedBackgroundException(typeof(T).Name + "Handler", nameof(HandleProcessableStepAsync), new($"Step: '{step.Name}' is not implemented'"));
}