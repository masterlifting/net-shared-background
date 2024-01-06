using Net.Shared.Background.Abstractions.Models;

namespace Net.Shared.Background.Abstractions.Interfaces;

public interface IBackgroundTask
{
    Task Run(BackgroundTaskModel model, CancellationToken cToken = default);
}