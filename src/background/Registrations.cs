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
    public static IServiceCollection AddBackgroundTasks(this IServiceCollection services, Action<BackgroundConfiguration> configure)
    {
        var configuration = new BackgroundConfiguration(services);
        
        configure.Invoke(configuration);

        if (configuration.Tasks.Count == 0)
            throw new InvalidOperationException("No tasks are configured.");

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
            .Validate(x => configuration.Tasks.Intersect(x.Tasks.Keys).Count() == configuration.Tasks.Count,
                "Some of the registered tasks are not configured")
            .Validate(x => !x.Tasks.Any(x => string.IsNullOrWhiteSpace(x.Value.Steps)),
                "Some of the registered tasks are not configured: steps are not set.");

        if (!configuration.IsSetProcessStepsRepository)
            throw new InvalidOperationException("Process Steps repository is not configured for the background tasks.");

        if (!configuration.IsSetProcessRepository)
            throw new InvalidOperationException("Process repository is not configured for the background tasks.");

        if (!configuration.IsSetConfigurationProvider)
            services.AddSingleton<IBackgroundSettingsProvider, OptionsMonitorSettingsProvider>();

        return services;
    }
}
