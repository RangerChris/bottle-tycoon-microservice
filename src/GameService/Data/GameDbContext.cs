using GameService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GameService.Data;

public class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options) : base(options)
    {
    }

    public DbSet<Player> Players { get; set; } = null!;
    public DbSet<Purchase> Purchases { get; set; } = null!;
    public DbSet<Upgrade> Upgrades { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Player>(entity =>
        {
            entity.ToTable("players");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Credits).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Purchase>(entity =>
        {
            entity.ToTable("purchases");
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Player).WithMany().HasForeignKey(e => e.PlayerId);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Upgrade>(entity =>
        {
            entity.ToTable("upgrades");
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Player).WithMany().HasForeignKey(e => e.PlayerId);
        });

        ApplyLowercaseNamingConvention(modelBuilder);
    }

    private static void ApplyLowercaseNamingConvention(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (!string.IsNullOrEmpty(tableName))
            {
                entityType.SetTableName(tableName.ToLowerInvariant());
            }

            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(property.Name.ToLowerInvariant());
            }

            foreach (var key in entityType.GetKeys())
            {
                if (!string.IsNullOrEmpty(key.GetName()))
                {
                    key.SetName(key.GetName()!.ToLowerInvariant());
                }
            }

            foreach (var index in entityType.GetIndexes())
            {
                if (!string.IsNullOrEmpty(index.GetDatabaseName()))
                {
                    index.SetDatabaseName(index.GetDatabaseName()!.ToLowerInvariant());
                }
            }

            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                if (!string.IsNullOrEmpty(foreignKey.GetConstraintName()))
                {
                    foreignKey.SetConstraintName(foreignKey.GetConstraintName()!.ToLowerInvariant());
                }
            }
        }
    }
}