using Microsoft.EntityFrameworkCore;

namespace HomeApp.Library.Database.EntityFramework;

public abstract class HomeAppDbContext<TContext> : DbContext
    where TContext : DbContext
{
    protected HomeAppDbContext(DbContextOptions<TContext> options)
        : base(options)
    {
    }

    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureModel(modelBuilder);
    }

    protected virtual void ConfigureModel(ModelBuilder modelBuilder)
    {
    }
}
