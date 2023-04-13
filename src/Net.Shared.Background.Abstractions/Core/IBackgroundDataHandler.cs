namespace Net.Shared.Background.Abstractions.Core;

public interface IBackgroundDataHandler<T> where T : class
{
    Task Handle(IEnumerable<T> data, CancellationToken cToken);
    Task<IReadOnlyCollection<T>> Handle(CancellationToken cToken);
}