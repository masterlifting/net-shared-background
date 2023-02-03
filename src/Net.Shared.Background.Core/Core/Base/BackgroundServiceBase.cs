using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Shared.Background.Exceptions;
using Shared.Background.Interfaces;
using Shared.Background.Settings;
using Shared.Background.Settings.Sections;
using Shared.Extensions.Logging;
using Shared.Persistence.Abstractions.Entities;

namespace Shared.Background.Core.Base;

public abstract class BackgroundServiceBase<T> : BackgroundService where T : class, IPersistentProcess
{
    private int _count;
    private const int Limit = 5_000;
    private const string Action = "BackgroundService";

    private Dictionary<string, BackgroundTaskSettings>? _tasks;

    private readonly ILogger _logger;
    private readonly IBackgroundTaskService _taskService;

    protected BackgroundServiceBase(
        IOptionsMonitor<BackgroundTaskSection> options
        , ILogger logger
        , IBackgroundTaskService taskService)
    {
        _tasks = options.CurrentValue.TaskSettings;
        options.OnChange(x => _tasks = x.TaskSettings);

        _taskService = taskService;
        _logger = logger;
    }
    protected override async Task ExecuteAsync(CancellationToken cToken)
    {
        if (_tasks is null || !_tasks.ContainsKey(_taskService.TaskName))
        {
            _logger.LogWarn(_taskService.TaskName, Action, Constants.Actions.NoConfig);
            await StopAsync(cToken);
            return;
        }

        var settings = _tasks[_taskService.TaskName];

        if (!settings.Scheduler.IsReady(out var readyInfo))
        {
            _logger.LogWarn(_taskService.TaskName, Action, readyInfo);
            await StopAsync(cToken);
            return;
        }

        var timerPeriod = TimeOnly.Parse(settings.Scheduler.WorkTime).ToTimeSpan();
        using var timer = new PeriodicTimer(timerPeriod);

        do
        {
            if (settings.Scheduler.IsStop(out var stopInfo))
            {
                _logger.LogWarn(_taskService.TaskName, Action, stopInfo);
                await StopAsync(cToken);
                return;
            }

            if (!settings.Scheduler.IsStart(out var startInfo))
            {
                _logger.LogWarn(_taskService.TaskName, Action, startInfo);
                continue;
            }

            try
            {
                if (_count == int.MaxValue)
                    _count = 0;

                _count++;

                _logger.LogTrace(_taskService.TaskName, Action, Constants.Actions.Start);

                if (settings.Steps.ProcessingMaxCount > Limit)
                {
                    settings.Steps.ProcessingMaxCount = Limit;

                    _logger.LogWarn(_taskService.TaskName, Action, Constants.Actions.Limit, Limit);
                }

                await _taskService.RunTaskAsync(_count, settings, cToken);

                _logger.LogTrace(_taskService.TaskName, Action, Constants.Actions.Done);
            }
            catch (Exception exception)
            {
                _logger.LogError(new SharedBackgroundException(_taskService.TaskName, Action, new(exception)));
            }
            finally
            {
                _logger.LogTrace(_taskService.TaskName, Action, Constants.Actions.NextStart + settings.Scheduler.WorkTime);

                if (settings.Scheduler.IsOnce)
                    settings.Scheduler.SetOnce();
            }
        } while (await timer.WaitForNextTickAsync(cToken));
    }
    public override async Task StopAsync(CancellationToken cToken)
    {
        _logger.LogWarn(_taskService.TaskName, Action, Constants.Actions.Stop);
        await base.StopAsync(cToken);
    }
}