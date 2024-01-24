using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Net.Shared.Background.Abstractions.Interfaces;
using Net.Shared.Background.Abstractions.Models.Settings;
using Net.Shared.Background.SettingProviders;
using Net.Shared.Persistence.Abstractions.Interfaces.Entities;

using static Net.Shared.Extensions.Serialization.Json.JsonExtensions;

namespace Net.Shared.Background;

public static class Registrations
{
    public static IServiceCollection AddBackgroundTasks(this IServiceCollection services, Assembly assembly, Action<BackgroundConfiguration>? configure = null)
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

        var backgroundTaskTypes = assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && IsSubclassOfRawGeneric(typeof(BackgroundTask<>), type))
            .ToImmutableArray();

        foreach (var backgroundTaskType in backgroundTaskTypes)
        {
            services.AddHostedService(serviceProvider => (BackgroundTask<IPersistentProcess>)ActivatorUtilities.CreateInstance(serviceProvider, backgroundTaskType));
        }

        return services;

        static bool IsSubclassOfRawGeneric(Type targetGenericType, Type? targetType)
        {
            while (targetType is not null && targetType != typeof(object))
            {
                var genericType = targetType.IsGenericType
                    ? targetType.GetGenericTypeDefinition()
                    : targetType;

                if (genericType == targetGenericType)
                    return true;

                targetType = targetType.BaseType;
            }
            return false;
        }
    }
}
