using Net.Shared.Background.Models.Settings;

namespace Net.Shared.Background.Schedulers
{
    public sealed class BackgroundTaskScheduler
    {
        public TimeOnly WorkTime { get; } = new TimeOnly(00, 10, 00);
        public List<DayOfWeek> WorkDays { get; } = new()
        {
            DayOfWeek.Monday
            , DayOfWeek.Monday
            , DayOfWeek.Monday
            , DayOfWeek.Monday
            , DayOfWeek.Monday
            , DayOfWeek.Monday
            , DayOfWeek.Monday
        };

        private bool _isOnce;
        private readonly TaskSchedulerSettings _settings;

        public BackgroundTaskScheduler(TaskSchedulerSettings settings)
        {
            _settings = settings;

            _isOnce = settings.IsOnce;

            WorkTime = TimeOnly.Parse(settings.WorkTime);

            if (!string.IsNullOrWhiteSpace(_settings.WorkDays))
            {
                var workDays = _settings.WorkDays.Split(",");
                WorkDays = new List<DayOfWeek>(workDays.Length);

                foreach (var number in workDays)
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

            if (!_settings.IsEnable)
            {
                info = $"disabled by setting: '{nameof(_settings.IsEnable)}'";
                return false;
            }

            if (!WorkDays.Contains(now.DayOfWeek))
            {
                info = $"the current day of week wasn't found in the setting: '{nameof(_settings.WorkDays)}'";
                return false;
            }

            return true;
        }
        public bool IsStart(out string info)
        {
            info = string.Empty;
            var now = DateTime.UtcNow;

            if (_settings.DateTimeStart > now)
            {
                info = $"the task's starting time '{nameof(_settings.DateTimeStart)}: {_settings.DateTimeStart: yyyy-MM-dd HH:mm:ss}' not already yet";
                return false;
            }

            return true;
        }
        public bool IsStop(out string info)
        {
            info = string.Empty;
            var now = DateTime.UtcNow;

            if (_settings.DateTimeStop < now)
            {
                info = $"the task's stopping time '{nameof(_settings.DateTimeStop)}: {_settings.DateTimeStop: yyyy-MM-dd HH:mm:ss}' has come";
                return true;
            }

            if (_isOnce)
            {
                info = $"the task is running once by setting: '{nameof(_settings.IsOnce)}'";
                return true;
            }

            return false;
        }
        public void SetOnce() => _isOnce = true;
    }
}
