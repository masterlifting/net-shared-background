namespace Net.Shared.Background.Abstractions.Settings.Sections;

public sealed class BackgroundTaskSection
{
    public const string Name = "Background";
    public Dictionary<string, BackgroundTaskSettings>? Tasks { get; set; }
}