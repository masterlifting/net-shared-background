using Net.Shared.Background.Models.Settings;

namespace Net.Shared.Background.Schedulers;

public sealed class BackgroundTaskScheduler
{
    public TimeOnly WorkTime { get; } = new TimeOnly(00, 10, 00);
    public List<int> WorkDays { get; } = new() { 1, 2, 3, 4, 5, 6, 7 };

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
                if (int.TryParse(number.Trim(), out var workDay))
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
        var today = (int)now.DayOfWeek;
        if (today == 0)
            today = 7;

        if (!WorkDays.Contains(today))
        {
            info = $"the current day of week wasn't found in the setting: '{nameof(_schedule.WorkDays)}'";
            return false;
        }

        return true;
    }
    public bool IsStart(out string info)
    {
        info = string.Empty;
        var now = DateTime.UtcNow;

        if (_schedule.DateTimeStart > now)
        {
            info = $"the task's starting time '{nameof(_schedule.DateTimeStart)}: {_schedule.DateTimeStart: yyyy-MM-dd HH:mm:ss}' not already yet";
            return false;
        }

        return true;
    }
    public bool IsStop(out string info)
    {
        info = string.Empty;
        var now = DateTime.UtcNow;

        if (_schedule.DateTimeStop < now)
        {
            info = $"the task's stopping time '{nameof(_schedule.DateTimeStop)}: {_schedule.DateTimeStop: yyyy-MM-dd HH:mm:ss}' has come";
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
