using System.Text.Json;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Net.Shared.Background.Abstractions.Interfaces;
using Net.Shared.Background.Abstractions.Models.Settings;
using Net.Shared.Background.ConfigurationProviders;

using static Net.Shared.Extensions.Serialization.Json.JsonExtensions;

namespace Net.Shared.Background;

public static partial class Registrations
{
    public static IServiceCollection AddBackgroundService<T>(this IServiceCollection services)
        where T : BackgroundService
    {
        services
            .AddSingleton(provider =>
            {
                var jsonOptions = new JsonSerializerOptions();

                jsonOptions.Converters.Add(new DateOnlyConverter());
                jsonOptions.Converters.Add(new TimeOnlyConverter());

                return jsonOptions;
            })
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
