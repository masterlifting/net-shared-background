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
    protected BackgroundService(string taskName, IBackgroundServiceConfigurationProvider provider, ILogger logger)
    {
        _log = logger;
        _taskName = taskName;
        _tasks = provider.Configuration.Tasks;
        provider.OnChange(x =>
        {
            _tasks = x.Tasks;
            _isConfigurationChanged = true;
        });
    }

    #region PRIVATE FIELDS
    private int _count;
    private readonly ILogger _log;
    private readonly string _taskName;
    private Dictionary<string, BackgroundTaskSettings>? _tasks;
    #endregion

    #region FUNCTIONS
    protected override async Task ExecuteAsync(CancellationToken cToken)
    {
    
    restart:
        
        _log.Warn($"The task '{_taskName}' was started.");

        if (_tasks?.ContainsKey(_taskName) != true)
        {
            _log.Warn($"The options was not found for the task '{_taskName}.'");
            await StopAsync(cToken);
            return;
        }

        var settings = _tasks[_taskName];
        var scheduler = new BackgroundTaskScheduler(settings.Schedule);

        if (!scheduler.IsReady(out var readyInfo))
        {
            _log.Warn($"The task '{_taskName}' was not ready because {readyInfo}.");
            await StopAsync(cToken);
            return;
        }

        var timerPeriod = scheduler.WorkTime.ToTimeSpan();
        var timer = new PeriodicTimer(timerPeriod);

        do
        {
            if (_isConfigurationChanged)
            {
                _isConfigurationChanged = false;
                _count = 0;
                _log.Warn($"The task '{_taskName}' will be restarted because the configuration was changed.");
                break;
            }

            if (scheduler.IsStop(out var stopInfo))
            {
                _log.Warn($"The task '{_taskName}' was stopped because {stopInfo}.");
                await StopAsync(cToken);
                return;
            }

            if (!scheduler.IsStart(out var startInfo))
            {
                _log.Warn($"The task '{_taskName}' was not started because {startInfo}.");
                continue;
            }

            if (_count == int.MaxValue)
            {
                _count = 0;

                _log.Warn($"The counter for the task '{_taskName}' was reset.");
            }

            _count++;

            try
            {
                _log.Trace($"Process for the task '{_taskName}' is started.");

                await Run(new(_taskName, _count, settings), cToken);

                _log.Trace($"Process for the task '{_taskName}' was done.");
            }
            catch (Exception exception)
            {
                _log.ErrorCompact(exception);
            }
            finally
            {
                _log.Trace($"The next task process '{_taskName}' will launch in {settings.Schedule.WorkTime}.");

                if (settings.Schedule.IsOnce)
                    scheduler.SetOnce();
            }
        } while (!cToken.IsCancellationRequested && await timer.WaitForNextTickAsync(cToken));

        timer.Dispose();

        goto restart;
    }
    public override async Task StopAsync(CancellationToken cToken)
    {
        await base.StopAsync(cToken);
        _log.Warn($"The task '{_taskName}' was stopped!");
    }
    #endregion

    #region ABSTRACT FUNCTIONS
    protected abstract Task Run(BackgroundTaskModel taskModel, CancellationToken cToken = default);
    #endregion
}
