using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Net.Shared.Background.Abstractions.Interfaces;
using Net.Shared.Background.Abstractions.Models.Settings;
using Net.Shared.Background.ConfigurationProviders;

namespace Net.Shared.Background;

public static partial class Registrations
{
    public static IServiceCollection AddBackgroundService<T>(this IServiceCollection services)
        where T : BackgroundService
    {
        services
           .AddOptions<BackgroundTasksConfiguration>()
           .Configure<IConfiguration>((settings, configuration) =>
           {
               configuration
                   .GetSection(BackgroundTasksConfiguration.SectionName)
                   .Bind(settings);
           });

        services.AddSingleton<IBackgroundServiceConfigurationProvider, BackgroundServiceOptionsProvider>();
        
        services.AddHostedService<T>();
        
        return services;
    }
}
