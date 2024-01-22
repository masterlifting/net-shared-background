namespace Net.Shared.Background.Abstractions.Models.Settings;

public sealed record BackgroundTaskSettings
{
    public int ChunkSize { get; init; } = 100;
    public bool IsParallel { get; init; }
    public bool IsInfinite { get; init; }
    public string[] Steps { get; init; } = [];

    public Guid HostId { get; init; } = new();
    public BackgroundTaskSchedule Schedule { get; init; } = new();
    public BackgroundTaskRetryPolicy? RetryPolicy { get; init; }
}
