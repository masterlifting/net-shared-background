namespace Net.Shared.Background.Abstractions.Core;

public interface IBackgroundDataHandler<T> where T : class
{
    /// <summary>
    /// Get data from source.
    /// </summary>
    /// <returns>
    /// Getted data.
    /// </returns>
    Task<IReadOnlyCollection<T>> Get(CancellationToken cToken);
    /// <summary>
    /// Process data.
    /// </summary>
    /// <param name="data">
    /// Processable data.
    /// </param>
    /// <returns>
    /// Task.
    /// </returns>
    Task Post(IEnumerable<T> data, CancellationToken cToken);
}