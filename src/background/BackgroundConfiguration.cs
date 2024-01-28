using Microsoft.Extensions.DependencyInjection;

using Net.Shared.Background.Abstractions.Interfaces;
using Net.Shared.Persistence.Abstractions.Interfaces.Entities;
using Net.Shared.Persistence.Abstractions.Interfaces.Entities.Catalogs;
using Net.Shared.Persistence.Abstractions.Interfaces.Repositories;

namespace Net.Shared.Background;

public sealed class BackgroundConfiguration(IServiceCollection services)
{
    public IServiceCollection Services { get; } = services;

    internal bool IsSetConfigurationProvider { get; private set; }
    internal bool IsSetStepsReaderRepository { get; private set; }
    internal bool IsSetProcessRepository { get; private set; }

    public void AddSettingsProvider<T>() where T : class, IBackgroundSettingsProvider
    {
        Services.AddTransient<IBackgroundSettingsProvider, T>();
        IsSetConfigurationProvider = true;
    }

    public void AddStepsReaderRepository<T, TRepo>() where T : class, IPersistentProcessStep where TRepo : class, IPersistenceReaderRepository<T>
    {
        Services.AddTransient<IPersistenceReaderRepository<T>, TRepo>();
        IsSetStepsReaderRepository = true;
    }
    
    public void AddProcessRepository<T, TRepo>() where T : class, IPersistentProcess where TRepo : class, IPersistenceProcessRepository<T>
    {
        Services.AddTransient<IPersistenceProcessRepository<T>, TRepo>();
        IsSetProcessRepository = true;
    }
}
