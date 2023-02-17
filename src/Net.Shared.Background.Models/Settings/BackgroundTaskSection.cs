namespace Net.Shared.Background.Models.Settings;

public sealed class BackgroundTaskSection
{
    public const string Name = "Background";
    public Dictionary<string, BackgroundTaskSettings>? Tasks { get; set; }
}