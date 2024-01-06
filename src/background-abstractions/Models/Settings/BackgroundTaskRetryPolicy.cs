namespace Net.Shared.Background.Abstractions.Models.Settings;

public sealed record BackgroundTaskRetryPolicy
{
    public int EveryTime { get; init; } = 5;
    public int MaxAttempts { get; init; } = 10;
}