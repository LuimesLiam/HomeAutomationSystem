using Microsoft.Extensions.Configuration;

namespace HomeApp.Host.Configuration;

public sealed class HostEnvironmentOptions
{
    public string[] FrontendAllowedOrigins { get; set; } = [];

    public static HostEnvironmentOptions FromConfiguration(IConfiguration configuration)
    {
        return new HostEnvironmentOptions
        {
            FrontendAllowedOrigins = SplitRawCsv(configuration["FRONTEND_ALLOWED_ORIGINS"])
        };
    }

    private static string[] SplitRawCsv(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
