using Microsoft.Extensions.Logging;

using Net.Shared.Background.Abstractions;
using Net.Shared.Background.Models;
using Net.Shared.Background.Models.Exceptions;
using Net.Shared.Background.Models.Settings;
using Net.Shared.Background.Schedulers;
using Net.Shared.Extensions;

namespace Net.Shared.Background.Core;

public abstract class BackgroundService : Microsoft.Extensions.Hosting.BackgroundService
{
    private bool _isConfigurationChanged;
    protected BackgroundService(string taskName, IBackgroundServiceConfigurationProvider provider, ILogger logger)
    {
        _logger = logger;
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
    private readonly ILogger _logger;
    private readonly string _taskName;
    private Dictionary<string, BackgroundTaskSettings>? _tasks;
    #endregion

    #region FUNCTIONS
    protected override async Task ExecuteAsync(CancellationToken cToken)
    {
    
    restart:
        
        //TODO: The task is started twice!
        _logger.Warning($"The task '{_taskName}' was started.");

        if (_tasks?.ContainsKey(_taskName) != true)
        {
            _logger.Warning($"The _options was not found for the task '{_taskName}.'");
            await StopAsync(cToken);
            return;
        }

        var settings = _tasks[_taskName];
        var scheduler = new BackgroundTaskScheduler(settings.Schedule);

        if (!scheduler.IsReady(out var readyInfo))
        {
            _logger.Warning($"The task '{_taskName}' was not ready because {readyInfo}.");
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
                _logger.Warning($"The task '{_taskName}' will be restarted because the configuration was changed.");
                break;
            }

            if (scheduler.IsStop(out var stopInfo))
            {
                _logger.Warning($"The task '{_taskName}' was stopped because {stopInfo}.");
                await StopAsync(cToken);
                return;
            }

            if (!scheduler.IsStart(out var startInfo))
            {
                _logger.Warning($"The task '{_taskName}' was not started because {startInfo}.");
                continue;
            }

            if (_count == int.MaxValue)
            {
                _count = 0;

                _logger.Warning($"The counter for the task '{_taskName}' was reset.");
            }

            _count++;

            try
            {
                _logger.Trace($"Process for the task '{_taskName}' is started.");

                await Run(new(_taskName, _count, settings), cToken);

                _logger.Trace($"Process for the task '{_taskName}' was done.");
            }
            catch (BackgroundException exception)
            {
                _logger.Error(exception);
            }
            catch (Exception exception)
            {
                _logger.Error(new BackgroundException(exception));
            }
            finally
            {
                _logger.Trace($"The next task process '{_taskName}' will launch in {settings.Schedule.WorkTime}.");

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
        _logger.Warning($"The task '{_taskName}' was stopped!");
    }
    #endregion

    #region ABSTRACT FUNCTIONS
    protected abstract Task Run(BackgroundTaskInfo taskInfo, CancellationToken cToken = default);
    #endregion
}