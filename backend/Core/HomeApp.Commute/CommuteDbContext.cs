using HomeApp.Library.Database.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace HomeApp.Commute;

public sealed class CommuteDbContext(DbContextOptions<CommuteDbContext> options) : HomeAppDbContext<CommuteDbContext>(options)
{
    public DbSet<WorkspaceRow> Workspaces => Set<WorkspaceRow>();
    public DbSet<PrivateMigrationRow> PrivateMigrations => Set<PrivateMigrationRow>();
    protected override void ConfigureModel(ModelBuilder model)
    {
        model.Entity<WorkspaceRow>(e => {
            e.ToTable("CommuteWorkspaces"); e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Json).IsRequired(); e.Property(x => x.Revision).IsConcurrencyToken();
        });
        model.Entity<PrivateMigrationRow>(e => {
            e.ToTable("CommutePrivateMigrations"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasMaxLength(240);
            e.Property(x => x.Hash).IsRequired();
        });
    }
}
public sealed class WorkspaceRow
{
    public int Id { get; set; } = 1;
    public string Json { get; set; } = "{}";
    public long Revision { get; set; }
}
public sealed class PrivateMigrationRow
{
    public string Id { get; set; } = "";
    public string Hash { get; set; } = "";
}
