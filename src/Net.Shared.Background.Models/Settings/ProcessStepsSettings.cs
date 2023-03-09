namespace Net.Shared.Background.Models.Settings;

public sealed record ProcessStepsSettings
{
    public int ProcessingMaxCount { get; set; }
    public bool IsParallelProcessing { get; init; }
    public string[] Names { get; init; } = Array.Empty<string>();
}