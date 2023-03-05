namespace Net.Shared.Background.Models.Settings;

public sealed record BackgroundTaskSettings
{
    public ProcessStepsSettings Steps { get; init; } = new();
    public TaskSchedulerSettings Scheduler { get; init; } = new();
    public TaskRetryPolicySettings? RetryPolicy { get; init; }
}