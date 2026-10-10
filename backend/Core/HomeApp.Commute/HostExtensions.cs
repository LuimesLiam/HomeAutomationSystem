using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using HomeApp.Library.Database.EntityFramework;

namespace HomeApp.Commute;
public static class HostExtensions
{
    public static IServiceCollection AddHomeAppCommute(this IServiceCollection services, IConfiguration config)
    {
        var connection = config["POSTGRES_CONNECTION"] ?? config["ConnectionStrings:DefaultConnection"];
        services.AddHomeAppPostgresDbContext<CommuteDbContext>(connection ?? throw new InvalidOperationException("Database connection is required."));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<RouteSettingsStore>();
        // Road traffic puts its credential in the query string. Suppress HTTP client request logging.
        services.AddHttpClient<IRoadTrafficProvider, RoadTrafficProvider>(c => c.Timeout = TimeSpan.FromSeconds(12))
            .UseSocketsHttpHandler((handler, _) => handler.PooledConnectionLifetime = TimeSpan.FromMinutes(10))
            .RemoveAllLoggers();
        services.AddHttpClient<IMarineTrafficProvider, OpenWatersProvider>(c => c.Timeout = TimeSpan.FromSeconds(12))
            .UseSocketsHttpHandler((handler, _) => handler.PooledConnectionLifetime = TimeSpan.FromMinutes(10));
        services.AddSingleton<ILiftBridgePredictor, LiftBridgePredictor>();
        services.AddSingleton<CommuteService>();
        services.AddHostedService<CommutePollingWorker>();
        services.AddControllers().AddApplicationPart(typeof(HostExtensions).Assembly);
        return services;
    }
}
