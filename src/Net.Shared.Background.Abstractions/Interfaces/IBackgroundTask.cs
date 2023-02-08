using Shared.Background.Settings;

namespace Net.Shared.Background.Abstractions.Interfaces;

public interface IBackgroundTaskService
{
    public string TaskName { get; }
    Task RunTaskAsync(int taskCount, BackgroundTaskSettings settings, CancellationToken cToken);
}