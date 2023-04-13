using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Net.Shared.Background.Abstractions.Core;
using Net.Shared.Background.Models;
using Net.Shared.Background.Models.Exceptions;
using Net.Shared.Background.Models.Settings;
using Net.Shared.Background.Schedulers;

namespace Net.Shared.Background.Core;

public abstract class NetSharedBackgroundService : BackgroundService
{
    protected NetSharedBackgroundService(string taskName, IBackgroundTaskConfigurationProvider provider, ILogger logger)
    {
        _logger = logger;
        _taskName = taskName;
        _tasks = provider.Configuration.Tasks;
        provider.OnChange(x => _tasks = x.Tasks);
    }

    #region PRIVATE FIELDS
    private int _count;
    private const int Limit = 5_000;
    private readonly ILogger _logger;
    private readonly string _taskName;
    private Dictionary<string, BackgroundTaskSettings>? _tasks;
    #endregion

    #region OVERRIDED FUNCTIONS
    protected override async Task ExecuteAsync(CancellationToken cToken)
    {
        if (_tasks?.ContainsKey(_taskName) != true)
        {
            _logger.LogWarn($"The configuration was not found for the task '{_taskName}.'");
            await StopAsync(cToken);
            return;
        }

        var settings = _tasks[_taskName];
        var scheduler = new BackgroundTaskScheduler(settings.Scheduler);

        if (!scheduler.IsReady(out var readyInfo))
        {
            _logger.LogWarn($"The task '{_taskName}' wasn't ready because {readyInfo}.");
            await StopAsync(cToken);
            return;
        }

        var timerPeriod = scheduler.WorkTime.ToTimeSpan();
        using var timer = new PeriodicTimer(timerPeriod);

        do
        {
            if (scheduler.IsStop(out var stopInfo))
            {
                _logger.LogWarn($"The task '{_taskName}' was stopped because {stopInfo}.");
                await StopAsync(cToken);
                return;
            }

            if (!scheduler.IsStart(out var startInfo))
            {
                _logger.LogWarn($"The task '{_taskName}' wasn't started because {stopInfo}.");
                continue;
            }

            if (_count == int.MaxValue)
            {
                _count = 0;

                _logger.LogWarn($"The counter for the task '{_taskName}' was reset.");
            }

            _count++;

            if (settings.Steps.ProcessingMaxCount > Limit)
            {
                settings.Steps.ProcessingMaxCount = Limit;

                _logger.LogWarn($"The limit of the processing data from the configuration for the task '{_taskName}' was exceeded and was set by default: {Limit}.");
            }

            try
            {
                _logger.LogTrace($"The task '{_taskName}' is starting...");

                await Run(new(_taskName, _count, settings), cToken);

                _logger.LogTrace($"The task '{_taskName}' was done!");
            }
            catch (NetSharedBackgroundException exception)
            {
                _logger.LogError(exception);
            }
            catch (Exception exception)
            {
                _logger.LogError(new NetSharedBackgroundException($"Unhandled exception: {exception.Message}"));
            }
            finally
            {
                _logger.LogTrace($"The next task '{_taskName}' will launch in {settings.Scheduler.WorkTime}.");

                if (settings.Scheduler.IsOnce)
                    scheduler.SetOnce();
            }
        } while (await timer.WaitForNextTickAsync(cToken));
    }
    public override async Task StopAsync(CancellationToken cToken)
    {
        _logger.LogWarn($"The task '{_taskName}' was stopped!");
        await base.StopAsync(cToken);
    }
    #endregion

    #region ABSTRACT FUNCTIONS
    protected abstract Task Run(NetSharedBackgroundTaskInfo taskInfo, CancellationToken cToken = default);
    #endregion
}