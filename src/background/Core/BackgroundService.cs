using Microsoft.Extensions.Logging;

using Net.Shared.Background.Abstractions.Interfaces;
using Net.Shared.Background.Abstractions.Models;
using Net.Shared.Background.Abstractions.Models.Settings;
using Net.Shared.Background.Schedulers;
using Net.Shared.Extensions.Logging;

namespace Net.Shared.Background.Core;

public abstract class BackgroundService : Microsoft.Extensions.Hosting.BackgroundService
{
    private bool _isConfigurationChanged;
    protected BackgroundService(string taskName, IBackgroundServiceConfigurationProvider configurationProvider, ILogger logger)
    {
        _log = logger;
        _taskName = taskName;
        _tasks = configurationProvider.Configuration.Tasks;
        configurationProvider.OnChange(x =>
        {
            _tasks = x.Tasks;
            _isConfigurationChanged = true;
        });
    }

    private int _count;
    private readonly ILogger _log;
    private readonly string _taskName;
    private Dictionary<string, BackgroundTaskSettings>? _tasks;
    
    protected override async Task ExecuteAsync(CancellationToken cToken)
    {

restart:

        _log.Warn($"Background process of the '{_taskName}' has started.");

        if (_tasks?.ContainsKey(_taskName) != true)
        {
            _log.ErrorShort(new InvalidOperationException($"Options for the '{_taskName}' was not found."));

            await StopAsync(cToken);

            return;
        }

        var taskSettings = _tasks[_taskName];
        var taskSchedule = new BackgroundTaskScheduler(taskSettings.Schedule);

        if (!taskSchedule.IsReady(out var notReadyReason))
        {
            _log.Warn($"Task '{_taskName}' is not ready. Reason: {notReadyReason}.");

            await StopAsync(cToken);

            return;
        }

        PeriodicTimer timer;

        do
        {
            timer = new PeriodicTimer(taskSchedule.WorkTime);

            if (_isConfigurationChanged)
            {
                _isConfigurationChanged = false;

                _count = 0;

                _log.Warn($"Configuration of the '{_taskName}' was changed. It will be restarted.");

                break;
            }

            if (taskSchedule.IsStop(out var stoppingReason))
            {
                _log.Warn($"Task '{_taskName}' has stopped. Reason: {stoppingReason}.");
                await StopAsync(cToken);
                return;
            }

            if (!taskSchedule.IsStart(out var startInfo, out var isNotStartingReason))
            {
                _log.Warn($"Task '{_taskName}' has not started. Reason: {startInfo}.");

                timer = new PeriodicTimer(isNotStartingReason);

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
                _log.Trace($"Process of the '{_taskName}' has started.");

                await Run(new(_taskName, _count, taskSettings), cToken);

                _log.Trace($"Process of the '{_taskName}' has been done.");
            }
            catch (Exception exception)
            {
                _log.ErrorCompact(exception);
            }
            finally
            {
                _log.Trace($"The '{_taskName}' next process will be launched in {taskSettings.Schedule.WorkTime}.");

                if (taskSettings.Schedule.IsOnce)
                    taskSchedule.SetOnce();
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

    protected abstract Task Run(BackgroundTask taskModel, CancellationToken cToken = default);
}
