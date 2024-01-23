using System.Reflection;
using System.Text.Json;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Net.Shared.Background.Abstractions.Interfaces;
using Net.Shared.Background.Abstractions.Models.Settings;
using Net.Shared.Background.SettingProviders;
using static Net.Shared.Extensions.Serialization.Json.JsonExtensions;

namespace Net.Shared.Background;

public static class Registrations
{
    public static IServiceCollection AddBackgroundServices(this IServiceCollection services, Assembly assembly, Action<BackgroundConfiguration>? configure = null)
    {
        services
            .AddSingleton(provider =>
            {
                var jsonOptions = new JsonSerializerOptions();

                jsonOptions.Converters.Add(new DateOnlyConverter());
                jsonOptions.Converters.Add(new TimeOnlyConverter());

                return jsonOptions;
            })
            .AddOptions<BackgroundSettings>()
            .Configure<IConfiguration>((settings, configuration) =>
            {
                configuration
                    .GetSection(BackgroundSettings.SectionName)
                    .Bind(settings);
            });

        var configuration = new BackgroundConfiguration(services);

        configure?.Invoke(configuration);

        if (!configuration.IsSetConfigurationProvider)
            services.AddSingleton<IBackgroundSettingsProvider, OptionsMonitorSettingsProvider>();

        // Find all classes that inherit from Net.Shared.Background.Service
        foreach (var serviceType in assembly.GetTypes().Where(type => type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(BackgroundService))))
        {
            services.AddHostedService(serviceProvider => (Microsoft.Extensions.Hosting.BackgroundService)ActivatorUtilities.CreateInstance(serviceProvider, serviceType));
        }

        return services;
    }
}
