using Microsoft.Extensions.Options;
using Net.Shared.Background.Abstractions.Core;
using Net.Shared.Background.Models.Settings;

namespace Net.Shared.Background.Core;

public class BackgroundTaskSettingsProvider : IBackgroundTaskConfigurationProvider
{
    private readonly IOptions<BackgroundTasksConfiguration> configuration;

    public BackgroundTaskSettingsProvider(IOptions<BackgroundTasksConfiguration> configuration)
    {
        Configuration = configuration.Value;
        this.configuration = configuration;
    }

    public BackgroundTasksConfiguration Configuration { get; }

    public void OnChange(Action<BackgroundTasksConfiguration> listener)
    {
        listener(Configuration);
    }
}