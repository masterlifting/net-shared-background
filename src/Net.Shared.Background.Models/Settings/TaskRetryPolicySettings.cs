namespace Net.Shared.Background.Models.Settings;

public sealed class TaskRetryPolicySettings
{
    public int EveryTime { get; set; } = 5;
    public int MaxAttempts { get; set; } = 10;
}