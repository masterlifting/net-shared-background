using Net.Shared.Background.Models.Settings;

namespace Net.Shared.Background.Abstractions.Core;

public interface IBackgroundServiceConfigurationProvider
{
    public BackgroundTasksConfiguration Configuration { get; }

    void OnChange(Action<BackgroundTasksConfiguration> action);
}
