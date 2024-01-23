using Microsoft.Extensions.Options;
using Net.Shared.Background.Abstractions.Interfaces;
using Net.Shared.Background.Abstractions.Models.Settings;

namespace Net.Shared.Background.SettingProviders;

public class OptionsMonitorSettingsProvider(IOptionsMonitor<BackgroundSettings> options) : IBackgroundSettingsProvider
{
    private readonly IOptionsMonitor<BackgroundSettings> _options = options;

    public BackgroundSettings Settings { get => _options.CurrentValue; }
    public void OnChange(Action<BackgroundSettings> listener) => _options.OnChange(listener);
}
