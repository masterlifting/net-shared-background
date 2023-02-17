namespace Net.Shared.Background.Models.Settings;

public sealed class TaskSchedulerSettings
{
    public bool IsEnable { get; set; } = false;
    public bool IsOnce { get; set; } = false;
    public string WorkDays { get; set; } = "1,2,3,4,5,6,7";
    public string WorkTime { get; set; } = "00:10:00";

    public DateTime DateTimeStart { get; set; } = DateTime.UtcNow;
    public DateTime? DateTimeStop { get; set; }
}