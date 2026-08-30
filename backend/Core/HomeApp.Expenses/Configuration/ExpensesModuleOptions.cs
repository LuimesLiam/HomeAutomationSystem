using Microsoft.Extensions.Configuration;

namespace HomeApp.Expenses.Configuration;

public sealed class ExpensesModuleOptions
{
    public string PostgresConnection { get; set; } = string.Empty;

    public static ExpensesModuleOptions FromConfiguration(IConfiguration configuration)
    {
        return new ExpensesModuleOptions
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
            $"Missing required expenses configuration value. Expected one of: {string.Join(", ", keys)}");
    }
}
