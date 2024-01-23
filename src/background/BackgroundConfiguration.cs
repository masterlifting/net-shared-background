using Microsoft.Extensions.DependencyInjection;

using Net.Shared.Background.Abstractions.Interfaces;

namespace Net.Shared.Background;

public sealed class BackgroundConfiguration(IServiceCollection services)
{
    private readonly IServiceCollection _services = services;

    internal bool IsSetConfigurationProvider { get; private set; }

    public void AddSettingsProvider<T>() where T : class, IBackgroundSettingsProvider
    {
        _services.AddTransient<IBackgroundSettingsProvider, T>();
        IsSetConfigurationProvider = true;
    }
}
