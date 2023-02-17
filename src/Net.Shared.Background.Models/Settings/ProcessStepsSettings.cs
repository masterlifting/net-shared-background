namespace Net.Shared.Background.Models.Settings;

public sealed class ProcessStepsSettings
{
    public int ProcessingMaxCount { get; set; }
    public bool IsParallelProcessing { get; set; }
    public string[] Names { get; set; } = Array.Empty<string>();
}