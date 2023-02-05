namespace Shared.Background.Settings.Models;

public sealed class ProcessStepsSettings
{
    public int ProcessingMaxCount { get; set; }
    public bool IsParallelProcessing { get; set; }
    public string[] Names { get; set; } = Array.Empty<string>();
}