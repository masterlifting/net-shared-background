using Net.Shared.Background.Abstractions.Models.Settings;

namespace Net.Shared.Background;

public sealed class BackgroundTaskScheduler
{
    private static readonly Dictionary<string, DayOfWeek> WorkDaysNumbersMapper = new(StringComparer.OrdinalIgnoreCase)
    {
        { "0", DayOfWeek.Sunday },
        { "1", DayOfWeek.Monday },
        { "2", DayOfWeek.Tuesday },
        { "3", DayOfWeek.Wednesday },
        { "4", DayOfWeek.Thursday },
        { "5", DayOfWeek.Friday },
        { "6", DayOfWeek.Saturday }
    };
    private static readonly Dictionary<string, DayOfWeek> WorkDaysLettersMapper = new(StringComparer.OrdinalIgnoreCase)
    {
        { "sun", DayOfWeek.Sunday },
        { "mon", DayOfWeek.Monday },
        { "tue", DayOfWeek.Tuesday },
        { "wed", DayOfWeek.Wednesday },
        { "thu", DayOfWeek.Thursday },
        { "fri", DayOfWeek.Friday },
        { "sat", DayOfWeek.Saturday }
    };
    private readonly List<DayOfWeek> _workDays = new(7);

    private readonly short _timeShift;
    private readonly bool _isEnable;
    private readonly DateTime _startWork;
    private readonly DateTime? _stopWork;
    private readonly TimeOnly _startTime;
    private readonly TimeOnly? _stopTime;
    
    private bool _setOnce;

    public TimeSpan WaitingPeriod { get; }

    public BackgroundTaskScheduler(BackgroundTaskSchedule schedule)
    {
        _timeShift = schedule.TimeShift;

        _isEnable = schedule.IsEnable;
        _setOnce = schedule.IsOnce;

        var now = DateTime.UtcNow.AddHours(_timeShift);

        _startWork = schedule.StartWork ?? now;
        _stopWork = schedule.StopWork;
                                                                     
        _startTime = schedule.StartTime ?? TimeOnly.FromDateTime(now);
        _stopTime = schedule.StopTime;

        WaitingPeriod = TimeOnly.Parse(schedule.WorkTime).ToTimeSpan();

        var workDays = schedule.WorkDays
            .Split(',')
            .Distinct()
            .Select(x => x.Trim())
            .ToArray();

        for (var i = 0; i < workDays.Length; i++)
        {
            if (WorkDaysNumbersMapper.TryGetValue(workDays[i], out var day))
            {
                _workDays.Add(day);
            }
            else if (WorkDaysLettersMapper.TryGetValue(workDays[i], out day))
            {
                _workDays.Add(day);
            }
            else
            {
                throw new ArgumentException($"Invalid work day: {workDays[i]}");
            }
        }
    }

    public bool IsStopped(out string? reason)
    {
        reason = null;

        if (!_isEnable)
        {
            reason = "is not enabled";
            return true;
        }

        // This condition is very important to avoid an infinite loop
        if (_workDays.Count == 0)
        {
            reason = "work days is empty";
            return true;
        }

        var now = DateTime.UtcNow.AddHours(_timeShift);

        if (_stopWork < now)
        {
            reason = $"work stopped at{_stopWork: yyyy-MM-dd HH:mm:ss}";
            return true;
        }

        if (_setOnce)
        {
            reason = "used once";
            return true;
        }

        return false;
    }
    public bool ReadyToStart(out string? reason, out TimeSpan waitingPeriod)
    {
        reason = null;
        waitingPeriod = WaitingPeriod;
        
        var now = DateTime.UtcNow.AddHours(_timeShift);

        if (!_workDays.Contains(now.DayOfWeek))
        {
            reason = $"day of week {now.DayOfWeek} is not enabled";

            var next = now;

            do
            {
                next = next.AddDays(1);
            }
            while (!_workDays.Contains(next.DayOfWeek));

            waitingPeriod = next.Date.Add(_startTime.ToTimeSpan()).Subtract(now);

            return false;
        }

        if (_startWork > now)
        {
            reason = $"time to start is{_startWork: yyyy-MM-dd HH:mm:ss}";

            waitingPeriod = _startWork.Subtract(now);

            return false;
        }

        if(_stopTime < TimeOnly.FromDateTime(now))
        {
            reason = $"time to stop is{_stopTime: HH:mm:ss}";

            waitingPeriod = now.Date.AddDays(1).Add(_startTime.ToTimeSpan()).Subtract(now);

            return false;
        }

        if(_startTime > TimeOnly.FromDateTime(now))
        {
            reason = $"time to start is{_startTime: HH:mm:ss}";

            waitingPeriod = now.Date.Add(_startTime.ToTimeSpan()).Subtract(now);

            return false;
        }

        return true;
    }
    public void SetOnce() => _setOnce = true;
}
