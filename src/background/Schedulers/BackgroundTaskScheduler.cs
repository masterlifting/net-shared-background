using Net.Shared.Background.Abstractions.Models.Settings;

namespace Net.Shared.Background.Schedulers;

public sealed class BackgroundTaskScheduler
{
    public TimeOnly WorkTime { get; private set; } = new(00, 10, 00);
    public List<DayOfWeek> WorkDays { get; } =
    [
        DayOfWeek.Monday
        , DayOfWeek.Thursday
        , DayOfWeek.Wednesday
        , DayOfWeek.Thursday
        , DayOfWeek.Friday
        , DayOfWeek.Saturday
        , DayOfWeek.Sunday
    ];

    private bool _isOnce;
    private readonly BackgroundTaskSchedule _schedule;

    public BackgroundTaskScheduler(BackgroundTaskSchedule schedule)
    {
        _schedule = schedule;

        _isOnce = schedule.IsOnce;

        WorkTime = TimeOnly.Parse(schedule.WorkTime);

        if (!string.IsNullOrWhiteSpace(_schedule.WorkDays))
        {
            WorkDays.Clear();

            foreach (var number in _schedule.WorkDays.Split(",").Distinct())
            {
                if (Enum.TryParse<DayOfWeek>(number.Trim(), out var workDay))
                    WorkDays.Add(workDay);
            }
        }
    }

    public bool IsReady(out string info)
    {
        info = string.Empty;
        var now = DateTime.UtcNow;

        if (!_schedule.IsEnable)
        {
            info = $"disabled by setting: '{nameof(_schedule.IsEnable)}'";
            return false;
        }

        if (!WorkDays.Contains(now.DayOfWeek))
        {
            info = $"the current day of week wasn't found in the setting: '{nameof(_schedule.WorkDays)}'";
            return false;
        }

        return true;
    }
    public bool IsStart(out string info, out TimeSpan period)
    {
        info = string.Empty;
        period = WorkTime.ToTimeSpan();

        var now = DateTime.UtcNow;

        if (_schedule.DateStart > DateOnly.FromDateTime(now))
        {
            info = $"the task's starting date '{nameof(_schedule.DateStart)}: {_schedule.DateStart: yyyy-MM-dd HH:mm:ss}' not already yet";

            period = _schedule.DateStart.ToDateTime(_schedule.TimeStart).Subtract(now);
            
            return false;
        }

        if (_schedule.TimeStart > TimeOnly.FromDateTime(now))
        {
            info = $"the task's starting time '{nameof(_schedule.TimeStart)}: {_schedule.TimeStart: HH:mm:ss}' not already yet";

            period = _schedule.DateStart.ToDateTime(_schedule.TimeStart).Subtract(now);

            return false;
        }

        return true;
    }
    public bool IsStop(out string info)
    {
        info = string.Empty;
        var now = DateTime.UtcNow;

        if (_schedule.DateStop < DateOnly.FromDateTime(now))
        {
            info = $"the task's stopping date '{nameof(_schedule.DateStop)}: {_schedule.DateStop: yyyy-MM-dd}' has come";
            return true;
        }

        if (_schedule.TimeStop < TimeOnly.FromDateTime(now))
        {
            info = $"the task's stopping time '{nameof(_schedule.TimeStop)}: {_schedule.TimeStop: HH:mm:ss}' has come";
            return true;
        }

        if (_isOnce)
        {
            info = $"the task is running once by setting: '{nameof(_schedule.IsOnce)}'";
            return true;
        }

        return false;
    }
    public void SetOnce() => _isOnce = true;
}
