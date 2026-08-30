using HomeApp.Library.Database.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace HomeApp.Videos.Data;

public sealed class HomeAppVideosDbContext : HomeAppDbContext<HomeAppVideosDbContext>
{
    public HomeAppVideosDbContext(DbContextOptions<HomeAppVideosDbContext> options)
        : base(options)
    {
    }

    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<Series> Series => Set<Series>();
    public DbSet<Episode> Episodes => Set<Episode>();
    public DbSet<MediaSource> MediaSources => Set<MediaSource>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Movie>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.FilePath).IsUnique();
            entity.HasIndex(e => new { e.Hidden, e.Title });
            entity.HasIndex(e => e.Genre);
            entity.HasIndex(e => e.Year);
        });

        modelBuilder.Entity<Series>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => new { e.Hidden, e.Title });
            entity.HasIndex(e => e.Genre);
        });

        modelBuilder.Entity<MediaSource>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SourceType).IsRequired();
            entity.Property(e => e.Path).IsRequired();
            entity.HasIndex(e => new { e.SourceType, e.Path }).IsUnique();
            entity.HasIndex(e => new { e.SourceType, e.DisplayOrder });
        });

        modelBuilder.Entity<Episode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.FilePath).IsUnique();
            entity.HasIndex(e => new { e.SeriesId, e.Season, e.EpisodeNumber });
            entity.HasIndex(e => e.Hidden);
            entity.HasOne(e => e.Series)
                .WithMany(s => s.Episodes)
                .HasForeignKey(e => e.SeriesId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
