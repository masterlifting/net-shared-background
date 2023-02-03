using Shared.Background.Settings;

namespace Shared.Background.Interfaces;

public interface IBackgroundTaskService
{
    public string TaskName { get; }
    Task RunTaskAsync(int taskCount, BackgroundTaskSettings settings, CancellationToken cToken);
}