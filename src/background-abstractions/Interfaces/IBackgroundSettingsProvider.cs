using Net.Shared.Background.Abstractions.Models.Settings;

namespace Net.Shared.Background.Abstractions.Interfaces;

public interface IBackgroundSettingsProvider
{
    BackgroundSettings Settings { get; }
    void OnChange(Action<BackgroundSettings> action);
}
