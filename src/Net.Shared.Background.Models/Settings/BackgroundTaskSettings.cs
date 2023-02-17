namespace Net.Shared.Background.Models.Settings;

public sealed class BackgroundTaskSettings
{
    public ProcessStepsSettings Steps { get; set; } = new();
    public TaskSchedulerSettings Scheduler { get; set; } = new();
    public TaskRetryPolicySettings? RetryPolicy { get; set; }
}