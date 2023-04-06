using Net.Shared.Background.Models.Settings;
using Net.Shared.Persistence.Abstractions.Entities;
using Net.Shared.Persistence.Abstractions.Entities.Catalogs;

namespace Net.Shared.Background.Abstractions.Core;

public interface IBackgroundTaskService
{
    public string TaskName { get; }
    Task StartTask<TStep, TData>(int iterator, BackgroundTaskSettings settings, CancellationToken cToken = default)
        where TStep : class, IPersistentProcessStep
        where TData : class, IPersistentProcess;
}