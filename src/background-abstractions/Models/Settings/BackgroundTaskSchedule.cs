namespace Net.Shared.Background.Abstractions.Models.Settings;

public sealed record BackgroundTaskSchedule
{
    public bool IsEnable { get; init; }
    public bool IsOnce { get; init; }
    public string WorkDays { get; init; } = "0,1,2,3,4,5,6";
    public string WorkTime { get; init; } = "00:10:00";

    public DateTime DateTimeStart { get; init; } = DateTime.UtcNow;
    public DateTime? DateTimeStop { get; init; }
}