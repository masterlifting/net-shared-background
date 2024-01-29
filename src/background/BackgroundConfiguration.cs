using Microsoft.Extensions.DependencyInjection;

using Net.Shared.Background.Abstractions.Interfaces;
using Net.Shared.Persistence.Abstractions.Interfaces.Entities;
using Net.Shared.Persistence.Abstractions.Interfaces.Entities.Catalogs;
using Net.Shared.Persistence.Abstractions.Interfaces.Repositories;

namespace Net.Shared.Background;

public sealed class BackgroundConfiguration(IServiceCollection services)
{
    private readonly IServiceCollection _services = services;

    internal List<string> Tasks { get; } = new(100);
    internal bool IsSetConfigurationProvider { get; private set; }
    internal bool IsSetStepsReaderRepository { get; private set; }
    internal bool IsSetProcessRepository { get; private set; }

    public void AddSettingsProvider<T>() where T : class, IBackgroundSettingsProvider
    {
        _services.AddSingleton<IBackgroundSettingsProvider, T>();
        IsSetConfigurationProvider = true;
    }

    public void AddStepsReaderRepository<T, TRepo>() where T : class, IPersistentProcessStep where TRepo : class, IPersistenceReaderRepository<T>
    {
        _services.AddScoped<IPersistenceReaderRepository<T>, TRepo>();
        IsSetStepsReaderRepository = true;
    }

    public void AddProcessRepository<T, TRepo>() where T : class, IPersistentProcess where TRepo : class, IPersistenceProcessRepository<T>
    {
        _services.AddScoped<IPersistenceProcessRepository<T>, TRepo>();
        IsSetProcessRepository = true;
    }

    public void AddTask<T>(string Name) where T : BackgroundService
    {
        Registrations.BackgroundTaskRegistrationsMap.Add(Name, new());
        Registrations.BackgroundTaskRegistrationsMap[Name].SetResult();

        Tasks.Add(Name);

        _services.AddHostedService<T>();
    }
}
