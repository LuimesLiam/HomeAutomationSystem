using HomeApp.Expenses.Configuration;
using HomeApp.Expenses.Data;
using HomeApp.Expenses.Services;
using HomeApp.Library.Database.EntityFramework;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HomeApp.Expenses;

public static class HostExtensions
{
    public static IServiceCollection AddHomeAppExpenses(
        this IServiceCollection services,
        IConfiguration config,
        ExpensesModuleOptions? options = null)
    {
        var expenseOptions = options ?? ExpensesModuleOptions.FromConfiguration(config);
        services.AddSingleton(expenseOptions);
        services.AddHomeAppPostgresDbContext<HomeAppExpensesDbContext>(expenseOptions.PostgresConnection);
        services.AddControllers()
            .AddApplicationPart(typeof(HostExtensions).Assembly);
        services.AddHttpClient();
        services.AddScoped<ExpenseService>();
        return services;
    }
}
