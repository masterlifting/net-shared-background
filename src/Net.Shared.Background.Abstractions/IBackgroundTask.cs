using Net.Shared.Background.Models;

namespace Net.Shared.Background.Abstractions;

public interface IBackgroundTask
{
    BackgroundTaskInfo TaskInfo { get; }
    Task Run(BackgroundTaskInfo info, CancellationToken cToken = default);
}