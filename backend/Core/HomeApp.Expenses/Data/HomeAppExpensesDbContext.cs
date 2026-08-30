using HomeApp.Library.Database.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace HomeApp.Expenses.Data;

public sealed class HomeAppExpensesDbContext : HomeAppDbContext<HomeAppExpensesDbContext>
{
    public HomeAppExpensesDbContext(DbContextOptions<HomeAppExpensesDbContext> options)
        : base(options)
    {
    }

    public DbSet<ExpenseEntry> ExpenseEntries => Set<ExpenseEntry>();
    public DbSet<ExpenseGroup> ExpenseGroups => Set<ExpenseGroup>();
    public DbSet<ExpenseAgentSettings> ExpenseAgentSettings => Set<ExpenseAgentSettings>();
    public DbSet<ExpenseLineItem> ExpenseLineItems => Set<ExpenseLineItem>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExpenseGroup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired();
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Color).IsRequired();
            entity.HasIndex(e => e.Key).IsUnique();
            entity.HasIndex(e => new { e.IsEnabled, e.DisplayOrder });
        });

        modelBuilder.Entity<ExpenseAgentSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DefaultCurrencyCode).IsRequired();
        });

        modelBuilder.Entity<ExpenseEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SourceType).IsRequired();
            entity.Property(e => e.CurrencyCode).IsRequired();
            entity.Property(e => e.ReceiptImageContentType);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.Property(e => e.SubtotalAmount).HasPrecision(18, 2);
            entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
            entity.Property(e => e.TipAmount).HasPrecision(18, 2);
            entity.Property(e => e.AnalysisConfidence).HasPrecision(5, 2);
            entity.HasIndex(e => e.ExpenseDate);
            entity.HasIndex(e => new { e.ExpenseDate, e.ExpenseGroupId });
            entity.HasIndex(e => e.DuplicateFingerprint);
            entity.HasIndex(e => e.CreatedAtUtc);
            entity.HasOne(e => e.ExpenseGroup)
                .WithMany(group => group.Expenses)
                .HasForeignKey(e => e.ExpenseGroupId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ExpenseLineItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(300);
            entity.Property(e => e.CanonicalName).IsRequired().HasMaxLength(300);
            entity.Property(e => e.NormalizedName).IsRequired().HasMaxLength(300);
            entity.Property(e => e.Unit).HasMaxLength(50);
            entity.Property(e => e.Quantity).HasPrecision(18, 4);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 4);
            entity.Property(e => e.TotalPrice).HasPrecision(18, 2);
            entity.HasIndex(e => e.NormalizedName);
            entity.HasIndex(e => new { e.ExpenseEntryId, e.NormalizedName });
            entity.HasOne(e => e.ExpenseEntry)
                .WithMany(expense => expense.LineItems)
                .HasForeignKey(e => e.ExpenseEntryId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
