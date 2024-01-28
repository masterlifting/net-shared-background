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
    internal static Dictionary<string,TaskCompletionSource> BackgroundRegistrationsMap = new(10);
    public static IServiceCollection AddBackgroundTask<T>(this IServiceCollection services, string Name, Action<BackgroundConfiguration> configure)
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
            .AddOptions<BackgroundSettings>()
            .Configure<IConfiguration>((settings, configuration) =>
            {
                configuration
                    .GetSection(BackgroundSettings.SectionName)
                    .Bind(settings);
            })
            .ValidateOnStart()
            .Validate(x => x.Tasks.ContainsKey(Name), $"Task '{Name}' is not configured.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.Tasks[Name].Steps), $"Steps of the task '{Name}' are not configured.");

        var configuration = new BackgroundConfiguration(services);

        configure.Invoke(configuration);

        if (!configuration.IsSetStepsReaderRepository)
            throw new InvalidOperationException($"Steps reader repository is not configured for the task '{Name}'.");

        if (!configuration.IsSetProcessRepository)
            throw new InvalidOperationException($"Process repository is not configured for the task '{Name}'.");

        if (!configuration.IsSetConfigurationProvider)
            services.AddSingleton<IBackgroundSettingsProvider, OptionsMonitorSettingsProvider>();

        services.AddHostedService<T>();

        BackgroundRegistrationsMap.Add(Name, new TaskCompletionSource());
        BackgroundRegistrationsMap[Name].SetResult();

        return services;
    }
}
