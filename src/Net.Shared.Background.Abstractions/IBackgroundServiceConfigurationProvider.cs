using Net.Shared.Background.Models.Settings;

namespace Net.Shared.Background.Abstractions;

public interface IBackgroundServiceConfigurationProvider
{
    BackgroundTasksConfiguration Configuration { get; }

    void OnChange(Action<BackgroundTasksConfiguration> action);
}
