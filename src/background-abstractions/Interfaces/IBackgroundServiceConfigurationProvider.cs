using Net.Shared.Background.Abstractions.Models.Settings;

namespace Net.Shared.Background.Abstractions.Interfaces;

public interface IBackgroundServiceConfigurationProvider
{
    BackgroundTasksConfiguration Configuration { get; }

    void OnChange(Action<BackgroundTasksConfiguration> action);
}
