namespace Net.Shared.Background.Abstractions.Models.Settings;

public sealed record BackgroundTaskSchedule
{
    public bool IsEnable { get; init; }
    public bool IsOnce { get; init; }
    public short TimeShift { get; init; } = +1; //hours

    public DateTime? StartWork { get; init; }
    public DateTime? StopWork { get; init; }

    public TimeOnly? StartTime { get; init; }
    public TimeOnly? StopTime { get; init; }

    public string WorkTime { get; init; } = "00:10:00";
    public string WorkDays { get; init; } = "0,1,2,3,4,5,6";
}
