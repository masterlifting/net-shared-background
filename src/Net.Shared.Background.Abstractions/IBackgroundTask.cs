using Net.Shared.Background.Models;

namespace Net.Shared.Background.Abstractions;

public interface IBackgroundTask
{
    Task Run(BackgroundTaskModel model, CancellationToken cToken = default);
}