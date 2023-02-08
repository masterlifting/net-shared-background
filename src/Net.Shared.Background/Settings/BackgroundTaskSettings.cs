using Net.Shared.Background.Settings.Models;

namespace Net.Shared.Background.Settings;

public sealed class BackgroundTaskSettings
{
    public ProcessStepsSettings Steps { get; set; } = new();
    public TaskSchedulerSettings Scheduler { get; set; } = new();
    public TaskRetryPolicySettings? RetryPolicy { get; set; }
}