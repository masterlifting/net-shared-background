namespace Net.Shared.Background.Abstractions.Models.Settings;

public sealed record BackgroundSettings
{
    public const string SectionName = "Background";
    public Dictionary<string, BackgroundTaskSettings> Tasks { get; init; } = [];
}
