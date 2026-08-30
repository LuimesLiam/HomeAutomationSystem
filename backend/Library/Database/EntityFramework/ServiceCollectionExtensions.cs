using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HomeApp.Library.Database.EntityFramework;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHomeAppPostgresDbContext<TContext>(
        this IServiceCollection services,
        string connectionString,
        Action<DbContextOptionsBuilder>? configureOptions = null)
        where TContext : DbContext
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return services.AddDbContext<TContext>(options =>
        {
            options.UseNpgsql(connectionString);
            configureOptions?.Invoke(options);
        });
    }
}
