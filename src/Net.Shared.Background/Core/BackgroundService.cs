using Microsoft.Extensions.Logging;
using Net.Shared.Background.Abstractions;
using Net.Shared.Background.Models;
using Net.Shared.Background.Models.Exceptions;
using Net.Shared.Background.Models.Settings;
using Net.Shared.Background.Schedulers;
using Net.Shared.Extensions.Logging;

namespace Net.Shared.Background.Core;

public abstract class BackgroundService : Microsoft.Extensions.Hosting.BackgroundService
{
    protected BackgroundService(string taskName, IBackgroundServiceConfigurationProvider provider, ILogger logger)
    {
        _logger = logger;
        _taskName = taskName;
        _tasks = provider.Configuration.Tasks;
        provider.OnChange(x => _tasks = x.Tasks);
    }

    #region PRIVATE FIELDS
    private int _count;
    private readonly ILogger _logger;
    private readonly string _taskName;
    private Dictionary<string, BackgroundTaskSettings>? _tasks;
    #endregion

    #region OVERRIDED FUNCTIONS
    protected override async Task ExecuteAsync(CancellationToken cToken)
    {
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
            _logger.Warning($"The task '{_taskName}' wasn't ready because {readyInfo}.");
            await StopAsync(cToken);
            return;
        }

        var timerPeriod = scheduler.WorkTime.ToTimeSpan();
        using var timer = new PeriodicTimer(timerPeriod);

        do
        {
            if (scheduler.IsStop(out var stopInfo))
            {
                _logger.Warning($"The task '{_taskName}' was stopped because {stopInfo}.");
                await StopAsync(cToken);
                return;
            }

            if (!scheduler.IsStart(out var startInfo))
            {
                _logger.Warning($"The task '{_taskName}' wasn't started because {stopInfo}.");
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
                _logger.Trace($"The task '{_taskName}' is starting...");

                await Run(new(_taskName, _count, settings), cToken);

                _logger.Trace($"The task '{_taskName}' was done!");
            }
            catch (BackgroundException exception)
            {
                _logger.Error(exception);
            }
            catch (Exception exception)
            {
                _logger.Error(new BackgroundException($"Unhandled exception: {exception.Message}"));
            }
            finally
            {
                _logger.Trace($"The next task '{_taskName}' will launch in {settings.Schedule.WorkTime}.");

                if (settings.Schedule.IsOnce)
                    scheduler.SetOnce();
            }
        } while (await timer.WaitForNextTickAsync(cToken));
    }
    public override async Task StopAsync(CancellationToken cToken)
    {
        _logger.Warning($"The task '{_taskName}' was stopped!");
        await base.StopAsync(cToken);
    }
    #endregion

    #region ABSTRACT FUNCTIONS
    protected abstract Task Run(BackgroundTaskInfo taskInfo, CancellationToken cToken = default);
    #endregion
}