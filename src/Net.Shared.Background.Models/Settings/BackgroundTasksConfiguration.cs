namespace Net.Shared.Background.Models.Settings;

public sealed record BackgroundTasksConfiguration
{
    public const string Name = "Background";
    public Dictionary<string, BackgroundTaskSettings>? Tasks { get; init; }
}