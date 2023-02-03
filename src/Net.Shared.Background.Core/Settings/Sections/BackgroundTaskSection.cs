namespace Shared.Background.Settings.Sections;

public sealed class BackgroundTaskSection
{
    public const string Name = "Background";
    public Dictionary<string, BackgroundTaskSettings>? TaskSettings { get; set; }
}