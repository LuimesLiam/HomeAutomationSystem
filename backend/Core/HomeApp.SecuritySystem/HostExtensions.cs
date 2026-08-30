using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HomeApp.SecuritySystem;

public static class HostExtensions
{
    public static IServiceCollection AddHomeAppSecuritySystem(
        this IServiceCollection services,
        IConfiguration config,
        SecuritySystemOptions? options = null)
    {
        services.AddSingleton(options ?? SecuritySystemOptions.FromConfiguration(config));
        services.AddSingleton<NotificationService>();
        services.AddHostedService<ZmqMotionListener>();
        return services;
    }
}
