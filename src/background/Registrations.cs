using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Net.Shared.Background.Abstractions.Interfaces;
using Net.Shared.Background.ConfigurationProviders;

namespace Net.Shared.Background;

public static partial class Registrations
{
    public static void ConfigureBackgroundServices(this IServiceCollection services, IConfiguration _)
    {
        services.AddSingleton<IBackgroundServiceConfigurationProvider, BackgroundServiceOptionsProvider>();
    }
}
