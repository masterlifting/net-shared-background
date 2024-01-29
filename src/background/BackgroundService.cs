using Microsoft.Extensions.Logging;

using Net.Shared.Background.Abstractions.Interfaces;
using Net.Shared.Background.Abstractions.Models.Settings;
using Net.Shared.Extensions.Logging;

namespace Net.Shared.Background;

public abstract class BackgroundService : Microsoft.Extensions.Hosting.BackgroundService
{
    protected BackgroundTaskSettings TaskSettings { get; private set; }
    protected string TaskName { get; }
    protected int RunCount { get; private set; }


    private readonly ILogger _log;

    private bool _isSettingsChanged;

    protected BackgroundService(string taskName, IBackgroundSettingsProvider settingsProvider, ILogger logger)
    {
        _log = logger;

        TaskName = taskName;

        TaskSettings = settingsProvider.Settings.Tasks[TaskName];

        settingsProvider.OnChange(x =>
        {
            TaskSettings = x.Tasks[TaskName];

            _isSettingsChanged = true;
        });
    }

    protected abstract Task Run(CancellationToken cToken);

    protected override async Task ExecuteAsync(CancellationToken cToken)
    {
        await Registrations.BackgroundTaskRegistrationsMap[TaskName].Task;

        _log.Info($"Background process of the '{TaskName}' has started.");

restart:

        var taskScheduler = new BackgroundTaskScheduler(TaskSettings.Schedule);

        var timer = new PeriodicTimer(taskScheduler.WaitingPeriod);

        do
        {
            if (_isSettingsChanged)
            {
                _isSettingsChanged = false;

                RunCount = 0;

                _log.Warn($"Configuration of the '{TaskName}' was changed. It will be restarted.");

                break;
            }

            if (taskScheduler.IsStopped(out var reason))
            {
                _log.Warn($"Task '{TaskName}' has stopped. Reason: {reason}.");

                await StopAsync(cToken);

                return;
            }

            if (!taskScheduler.ReadyToStart(out reason, out var waitingPeriod))
            {
                timer = new PeriodicTimer(waitingPeriod);

                _log.Warn($"Task '{TaskName}' is not ready to start. Reason: {reason}. Next time the task '{TaskName}' will be launched in {waitingPeriod:dd\\.hh\\:mm\\:ss}.");

                continue;
            }

            if (RunCount == int.MaxValue)
            {
                RunCount = 0;

                _log.Warn($"Counter for the task '{TaskName}' was reset.");
            }

            RunCount++;

            try
            {
                _log.Trace($"Task '{TaskName}' has started.");

                await Run(cToken);

                _log.Debug($"Task '{TaskName}' has finished.");
            }
            catch (Exception exception)
            {
                _log.ErrorFull(exception);
            }
            finally
            {
                _log.Trace($"Next time the task '{TaskName}' will be launched in {taskScheduler.WaitingPeriod:dd\\.hh\\:mm\\:ss}.");

                if (TaskSettings.Schedule.IsOnce)
                    taskScheduler.SetOnce();
            }
        } while (!cToken.IsCancellationRequested && await timer.WaitForNextTickAsync(cToken));

        timer.Dispose();

        if (!cToken.IsCancellationRequested)
            goto restart;
    }
    public override async Task StopAsync(CancellationToken cToken)
    {
        await base.StopAsync(cToken);

        _log.Info($"Background service of the '{TaskName}' has been stopped.");
    }
}
