using Net.Shared.Background.Models.Settings;

namespace Net.Shared.Background.Abstractions;

public interface IBackgroundServiceConfigurationProvider
{
    public BackgroundTasksConfiguration Configuration { get; }

    void OnChange(Action<BackgroundTasksConfiguration> action);
}
