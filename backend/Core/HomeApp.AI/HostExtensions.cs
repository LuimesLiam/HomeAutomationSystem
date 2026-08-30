using HomeApp.AI.Data;
using HomeApp.AI.Services;
using HomeApp.AI.Configuration;
using HomeApp.Library.Database.EntityFramework;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HomeApp.AI;

public static class HostExtensions
{
    public static IServiceCollection AddHomeAppAi(
        this IServiceCollection services,
        IConfiguration config,
        AiModuleOptions? options = null)
    {
        var aiOptions = options ?? AiModuleOptions.FromConfiguration(config);
        services.AddSingleton(aiOptions);
        services.AddHomeAppPostgresDbContext<HomeAppAiDbContext>(aiOptions.PostgresConnection);
        services.AddControllers()
            .AddApplicationPart(typeof(HostExtensions).Assembly);
        services.AddScoped<AiSettingsService>();
        services.AddScoped<AiChatService>();
        services.AddScoped<IHomeAppAgentModelCatalog>(services => services.GetRequiredService<AiSettingsService>());
        services.AddScoped<IHomeAppAgentFactory, HomeAppAgentFactory>();
        return services;
    }
}
