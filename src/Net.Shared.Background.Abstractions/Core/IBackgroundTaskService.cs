using Net.Shared.Background.Models.Settings;

namespace Net.Shared.Background.Abstractions.Core;

public interface IBackgroundTaskService
{
    public string TaskName { get; }
    Task StartTask(int iterator, BackgroundTaskSettings settings, CancellationToken cToken = default);
}