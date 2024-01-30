using Microsoft.Extensions.DependencyInjection;

using Net.Shared.Background.Abstractions.Interfaces;
using Net.Shared.Persistence.Abstractions.Interfaces.Entities;
using Net.Shared.Persistence.Abstractions.Interfaces.Entities.Catalogs;
using Net.Shared.Persistence.Abstractions.Interfaces.Repositories;

namespace Net.Shared.Background;

public sealed class BackgroundConfiguration(IServiceCollection services)
{
    private readonly IServiceCollection _services = services;

    internal HashSet<string> Tasks { get; } = new(100);
    internal bool IsSetConfigurationProvider { get; private set; }
    internal bool IsSetProcessStepsRepository { get; private set; }
    internal bool IsSetProcessRepository { get; private set; }

    public void AddSettingsProvider<T>() where T : class, IBackgroundSettingsProvider
    {
        _services.AddSingleton<IBackgroundSettingsProvider, T>();
        IsSetConfigurationProvider = true;
    }

    public void AddProcessStepsRepository<T, TRepo>() where T : class, IPersistentProcessStep where TRepo : class, IPersistenceReaderRepository<T>
    {
        _services.AddScoped<IPersistenceReaderRepository<T>, TRepo>();
        IsSetProcessStepsRepository = true;
    }

    public void AddProcessRepository<T, TRepo>() where T : class, IPersistentProcess where TRepo : class, IPersistenceProcessRepository<T>
    {
        _services.AddScoped<IPersistenceProcessRepository<T>, TRepo>();
        IsSetProcessRepository = true;
    }

    public void AddTask<T>(string Name) where T : BackgroundService
    {
        var result = Tasks.Add(Name);

        if (!result)
            throw new InvalidOperationException($"Task with name {Name} is already registered.");

        _services.AddHostedService<T>();
    }
}
