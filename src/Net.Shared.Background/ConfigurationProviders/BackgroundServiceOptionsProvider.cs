using Microsoft.Extensions.Options;
using Net.Shared.Background.Abstractions.Core;
using Net.Shared.Background.Models.Settings;

namespace Net.Shared.Background.Core;

public class BackgroundServiceOptionsProvider : IBackgroundServiceConfigurationProvider
{
    private readonly IOptionsMonitor<BackgroundTasksConfiguration> _options;

    public BackgroundServiceOptionsProvider(IOptionsMonitor<BackgroundTasksConfiguration> options)
    {
        _options = options;
    }

    public BackgroundTasksConfiguration Configuration { get => _options.CurrentValue; }
    public void OnChange(Action<BackgroundTasksConfiguration> listener) => _options.OnChange(listener);
}