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
    internal static Dictionary<string, TaskCompletionSource> BackgroundTaskRegistrationsMap = new(100);

    public static IServiceCollection AddBackgroundTasks(this IServiceCollection services, Action<BackgroundConfiguration> configure)
    {
        var configuration = new BackgroundConfiguration(services);

        if (configuration.Tasks.Count == 0)
            throw new InvalidOperationException("No tasks are configured.");

        configure.Invoke(configuration);

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
            .Validate(x =>
                x.Tasks.Keys.All(BackgroundTaskRegistrationsMap.Keys.Contains),
                $"Some of the tasks are not configured: {string.Join(", ", BackgroundTaskRegistrationsMap.Keys)}.")
            .Validate(x =>
            {
                foreach (var task in x.Tasks.Join(BackgroundTaskRegistrationsMap, x => x.Key, y => y.Key, (x, _) => x))
                {
                    if (string.IsNullOrWhiteSpace(task.Value.Steps))
                        return false;
                }

                return true;
            }, "Some of the tasks are not configured: steps are not set.");


        if (!configuration.IsSetStepsReaderRepository)
            throw new InvalidOperationException($"Steps reader repository is not configured for the tasks '{string.Join(", ", BackgroundTaskRegistrationsMap.Keys)}'.");

        if (!configuration.IsSetProcessRepository)
            throw new InvalidOperationException($"Process repository is not configured for the tasks '{string.Join(", ", BackgroundTaskRegistrationsMap.Keys)}'.");

        if (!configuration.IsSetConfigurationProvider)
            services.AddSingleton<IBackgroundSettingsProvider, OptionsMonitorSettingsProvider>();

        return services;
    }
}
