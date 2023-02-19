using Net.Shared.Background.Models.Settings;

namespace Net.Shared.Background.Abstractions;

public interface IBackgroundTaskService
{
    public string TaskName { get; }
    Task RunTaskAsync(int taskCount, BackgroundTaskSettings settings, CancellationToken cToken);
}