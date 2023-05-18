using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Net.Shared.Background.Abstractions;
using Net.Shared.Background.Core;

namespace Net.Shared.Background;

public static partial class Registrations
{
    public static void ConfigureBackgroundServices(this IServiceCollection services, IConfiguration _)
    {
        services.AddSingleton<IBackgroundServiceConfigurationProvider, BackgroundServiceOptionsProvider>();
    }
}
