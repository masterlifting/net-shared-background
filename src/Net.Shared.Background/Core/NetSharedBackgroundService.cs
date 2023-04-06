using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Net.Shared.Background.Abstractions.Core;
using Net.Shared.Background.Models.Exceptions;
using Net.Shared.Background.Models.Settings;
using Net.Shared.Background.Schedulers;
using Net.Shared.Persistence.Abstractions.Entities;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;

namespace Net.Shared.Background.Core;

public abstract class NetSharedBackgroundService<TStep, TData> : BackgroundService
    where TStep : class, IPersistentProcessStep
    where TData : class, IPersistentProcess
{
    private int _count;
    private const int Limit = 5_000;

    private Dictionary<string, BackgroundTaskSettings>? _tasks;

    private readonly ILogger _logger;
    private readonly IBackgroundTaskService _taskService;

    protected NetSharedBackgroundService(IOptionsMonitor<BackgroundTaskSection> options, ILogger logger, IBackgroundTaskService taskService)
    {
        _tasks = options.CurrentValue.Tasks;
        options.OnChange(x => _tasks = x.Tasks);

        _taskService = taskService;
        _logger = logger;
    }
    protected override async Task ExecuteAsync(CancellationToken cToken)
    {
        if (_tasks?.ContainsKey(_taskService.TaskName) != true)
        {
            _logger.LogWarn($"The configuration was not found for the task '{_taskService.TaskName}.'");
            await StopAsync(cToken);
            return;
        }

        var settings = _tasks[_taskService.TaskName];
        var scheduler = new BackgroundTaskScheduler(settings.Scheduler);

        if (!scheduler.IsReady(out var readyInfo))
        {
            _logger.LogWarn($"The task '{_taskService.TaskName}' wasn't ready because {readyInfo}.");
            await StopAsync(cToken);
            return;
        }

        var timerPeriod = scheduler.WorkTime.ToTimeSpan();
        using var timer = new PeriodicTimer(timerPeriod);

        do
        {
            if (scheduler.IsStop(out var stopInfo))
            {
                _logger.LogWarn($"The task '{_taskService.TaskName}' was stopped because {stopInfo}.");
                await StopAsync(cToken);
                return;
            }

            if (!scheduler.IsStart(out var startInfo))
            {
                _logger.LogWarn($"The task '{_taskService.TaskName}' wasn't started because {stopInfo}.");
                continue;
            }

            if (_count == int.MaxValue)
            {
                _count = 0;

                _logger.LogWarn($"The counter for the task '{_taskService.TaskName}' was reset.");
            }

            _count++;

            if (settings.Steps.ProcessingMaxCount > Limit)
            {
                settings.Steps.ProcessingMaxCount = Limit;

                _logger.LogWarn($"The limit of the processing data from the configuration for the task '{_taskService.TaskName}' was exceeded and was set by default: {Limit}.");
            }

            try
            {
                _logger.LogTrace($"The task '{_taskService.TaskName}' is starting...");

                await _taskService.StartTask<TStep, TData>(_count, settings, cToken);

                _logger.LogTrace($"The task '{_taskService.TaskName}' was done!");
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
                _logger.LogTrace($"The next task '{_taskService.TaskName}' will launch in {settings.Scheduler.WorkTime}.");

                if (settings.Scheduler.IsOnce)
                    scheduler.SetOnce();
            }
        } while (await timer.WaitForNextTickAsync(cToken));
    }
    public override async Task StopAsync(CancellationToken cToken)
    {
        _logger.LogWarn($"The task '{_taskService.TaskName}' was stopped!");
        await base.StopAsync(cToken);
    }
}