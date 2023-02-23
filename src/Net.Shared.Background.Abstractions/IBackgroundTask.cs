using Net.Shared.Background.Models.Settings;

namespace Net.Shared.Background.Abstractions;

public interface IBackgroundTaskService
{
    public string Name { get; }
    Task RunAsync(int iterator, BackgroundTaskSettings settings, CancellationToken cToken);
}