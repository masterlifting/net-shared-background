using Microsoft.Extensions.Logging;

using Net.Shared.Background.Abstractions.Interfaces;
using Net.Shared.Background.Abstractions.Models;
using Net.Shared.Background.Abstractions.Models.Settings;
using Net.Shared.Extensions.Logging;

namespace Net.Shared.Background;

public abstract class BackgroundService : Microsoft.Extensions.Hosting.BackgroundService
{
    private bool _isSettingsChanged;
    protected BackgroundService(string taskName, IBackgroundSettingsProvider settingsProvider, ILogger logger)
    {
        _log = logger;
        _taskName = taskName;

        _tasks = settingsProvider.Settings.Tasks;

        settingsProvider.OnChange(x =>
        {
            _tasks = x.Tasks;
            _isSettingsChanged = true;
        });
    }

    private int _count;
    private readonly ILogger _log;
    private readonly string _taskName;
    private Dictionary<string, BackgroundTaskSettings>? _tasks;

    protected override async Task ExecuteAsync(CancellationToken cToken)
    {
        if (cToken.IsCancellationRequested)
            return;

restart:

        _log.Warn($"Background process of the '{_taskName}' has started.");

        if (_tasks?.ContainsKey(_taskName) != true)
        {
            _log.Warn($"Task '{_taskName}' was not found in the configuration.");

            await StopAsync(cToken);

            return;
        }

        var taskSettings = _tasks[_taskName];
        var taskScheduler = new BackgroundTaskScheduler(taskSettings.Schedule);

        var timer = new PeriodicTimer(taskScheduler.WaitingPeriod);

        do
        {
            if (_isSettingsChanged)
            {
                _isSettingsChanged = false;

                _count = 0;

                _log.Warn($"Configuration of the '{_taskName}' was changed. It will be restarted.");

                break;
            }

            if (taskScheduler.IsStopped(out var reason))
            {
                _log.Warn($"Task '{_taskName}' has stopped. Reason: {reason}.");

                await StopAsync(cToken);

                return;
            }

            if (!taskScheduler.ReadyToStart(out reason, out var waitingPeriod))
            {
                _log.Warn($"Task '{_taskName}' is not ready to start. Reason: {reason}.");

                timer = new PeriodicTimer(waitingPeriod);

                _log.Warn($"Next time the task '{_taskName}' will be launched in {waitingPeriod:dd\\.hh\\:mm\\:ss}.");

                continue;
            }

            if (_count == int.MaxValue)
            {
                _count = 0;

                _log.Warn($"Counter for the task '{_taskName}' was reset.");
            }

            _count++;

            try
            {
                _log.Trace($"Task '{_taskName}' has started.");

                await Start(new(_taskName, _count, taskSettings), cToken);

                _log.Trace($"Task '{_taskName}' has finished.");
            }
            catch (Exception exception)
            {
                _log.ErrorFull(exception);
            }
            finally
            {
                _log.Trace($"Next time the task '{_taskName}' will be launched in {taskScheduler.WaitingPeriod:dd\\.hh\\:mm\\:ss}.");

                if (taskSettings.Schedule.IsOnce)
                    taskScheduler.SetOnce();
            }
        } while (!cToken.IsCancellationRequested && await timer.WaitForNextTickAsync(cToken));

        timer.Dispose();

        goto restart;
    }
    public override async Task StopAsync(CancellationToken cToken)
    {
        await base.StopAsync(cToken);

        _log.Warn($"Background service of the '{_taskName}' has been stopped.");
    }

    protected abstract Task Start(BackgroundTask task, CancellationToken cToken = default);
}
