using Microsoft.Extensions.Configuration;

namespace HomeApp.AI.Configuration;

public sealed class AiModuleOptions
{
    public string PostgresConnection { get; set; } = string.Empty;

    public static AiModuleOptions FromConfiguration(IConfiguration configuration)
    {
        return new AiModuleOptions
        {
            PostgresConnection = GetRequiredValue(
                configuration,
                "POSTGRES_CONNECTION",
                "ConnectionStrings:DefaultConnection")
        };
    }

    private static string GetRequiredValue(IConfiguration configuration, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = configuration[key]?.Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        throw new InvalidOperationException(
            $"Missing required AI configuration value. Expected one of: {string.Join(", ", keys)}");
    }
}
