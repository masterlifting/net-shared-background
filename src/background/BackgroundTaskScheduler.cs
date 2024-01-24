﻿using Net.Shared.Background.Abstractions.Models.Settings;

namespace Net.Shared.Background;

public sealed class BackgroundTaskScheduler
{
    public TimeSpan WorkTime { get; private set; } = new(00, 10, 00);
    public DayOfWeek[] WorkDays { get; } =
    [
        DayOfWeek.Monday,
        DayOfWeek.Thursday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
        DayOfWeek.Saturday,
        DayOfWeek.Sunday
    ];

    private bool _setOnce;
    private readonly BackgroundTaskSchedule _schedule;

    public BackgroundTaskScheduler(BackgroundTaskSchedule schedule)
    {
        _schedule = schedule;

        _setOnce = schedule.IsOnce;

        WorkTime = TimeOnly.Parse(schedule.WorkTime).ToTimeSpan();

        if (!string.IsNullOrWhiteSpace(_schedule.WorkDays))
        {
            var workDaysNumbers = _schedule.WorkDays.Split(",").Distinct().ToArray();

            WorkDays = new DayOfWeek[workDaysNumbers.Length];

            for (var i = 0; i < workDaysNumbers.Length; i++)
            {
                if (Enum.TryParse<DayOfWeek>(workDaysNumbers[i].Trim(), out var workDay))
                    WorkDays[i] = workDay;
            }
        }
    }

    public bool IsReady(out string? reason)
    {
        reason = null;

        if (!_schedule.IsEnable)
        {
            reason = "is not enabled";
            return false;
        }

        // This condition is very important to avoid an infinite loop
        if(WorkDays.Length == 0)
        {
            reason = "work days is empty";
            return false;
        }

        return true;
    }
    public bool IsStart(out string? reason, out TimeSpan wakeupPeriod)
    {
        reason = null;
        wakeupPeriod = WorkTime;

        var now = DateTime.UtcNow;

        if (!WorkDays.Contains(now.DayOfWeek))
        {
            reason = $"day of week {now.DayOfWeek} is not enabled";

            var next = now;
            
            do
            {
                next = next.AddDays(1);
            }
            while (!WorkDays.Contains(next.DayOfWeek));

            wakeupPeriod = next.Subtract(now);
            
            return false;
        }

        if (_schedule.DateStart > DateOnly.FromDateTime(now))
        {
            reason = $"date start is{_schedule.DateStart: yyyy-MM-dd}";

            wakeupPeriod = _schedule.DateStart.ToDateTime(_schedule.TimeStart).Subtract(now);

            return false;
        }

        if (_schedule.TimeStart > TimeOnly.FromDateTime(now))
        {
            reason = $"time start is{_schedule.TimeStart: HH:mm:ss}";

            wakeupPeriod = _schedule.DateStart.ToDateTime(_schedule.TimeStart).Subtract(now);

            return false;
        }

        return true;
    }
    public bool IsStop(out string? reason)
    {
        reason = null;
        var now = DateTime.UtcNow;

        if (_schedule.DateStop < DateOnly.FromDateTime(now))
        {
            if (_schedule.TimeStop.HasValue)
            {
                if(_schedule.TimeStop.Value < TimeOnly.FromDateTime(now))
                {
                    reason = $"time stop is{_schedule.TimeStop: HH:mm:ss} on {_schedule.DateStop: yyyy-MM-dd}";
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                reason = $"date stop is{_schedule.DateStop: yyyy-MM-dd}";
                return true;
            }
        }

        if (_schedule.TimeStop < TimeOnly.FromDateTime(now))
        {
            reason = $"time stop is{_schedule.TimeStop: HH:mm:ss}";
            return true;
        }

        if (_setOnce)
        {
            reason = "used once";
            return true;
        }

        return false;
    }
    public void SetOnce() => _setOnce = true;
}
