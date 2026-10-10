using HomeApp.Library.Database.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace HomeApp.AI.Data;

public sealed class HomeAppAiDbContext : HomeAppDbContext<HomeAppAiDbContext>
{
    public HomeAppAiDbContext(DbContextOptions<HomeAppAiDbContext> options)
        : base(options)
    {
    }

    public DbSet<LlmConfiguration> Llms => Set<LlmConfiguration>();
    public DbSet<AiChatSession> AiChatSessions => Set<AiChatSession>();
    public DbSet<AiChatSessionMessage> AiChatSessionMessages => Set<AiChatSessionMessage>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LlmConfiguration>(entity =>
        {
            entity.ToTable("llm");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired();
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.ModelName).IsRequired();
            entity.Property(e => e.Provider).IsRequired();
            entity.Property(e => e.BaseUrl).IsRequired();
            entity.Property(e => e.ApiKeyName).IsRequired();
            entity.Property(e => e.ParamsJson).HasColumnType("jsonb");
            entity.HasIndex(e => e.Key).IsUnique();
            entity.HasIndex(e => new { e.Provider, e.ModelName });
        });

        modelBuilder.Entity<AiChatSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired();
            entity.Property(e => e.ModelKey).IsRequired();
            entity.HasIndex(e => e.UpdatedAtUtc);
        });

        modelBuilder.Entity<AiChatSessionMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Role).IsRequired();
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.ModelKey).IsRequired();
            entity.Property(e => e.ModelName).IsRequired();
            entity.Property(e => e.ProviderName).IsRequired();
            entity.HasIndex(e => new { e.SessionId, e.DisplayOrder });
            entity.HasOne(e => e.Session)
                .WithMany(e => e.Messages)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
