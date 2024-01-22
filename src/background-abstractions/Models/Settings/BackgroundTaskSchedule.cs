namespace Net.Shared.Background.Abstractions.Models.Settings;

public sealed record BackgroundTaskSchedule
{
    public bool IsEnable { get; init; }
    public bool IsOnce { get; init; }

    public TimeOnly TimeStart { get; init; } = TimeOnly.FromDateTime(DateTime.UtcNow);
    public TimeOnly? TimeStop { get; init; }
    public string WorkTime { get; init; } = "00:10:00";

    public DateOnly DateStart { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly? DateStop { get; init; }
    public string WorkDays { get; init; } = "0,1,2,3,4,5,6";
}
