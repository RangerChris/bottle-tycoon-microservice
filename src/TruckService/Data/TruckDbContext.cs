using Microsoft.EntityFrameworkCore;

namespace TruckService.Data;

public class TruckDbContext : DbContext
{
    public TruckDbContext(DbContextOptions<TruckDbContext> opts) : base(opts)
    {
    }

    public DbSet<TruckEntity> Trucks { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TruckEntity>().HasKey(t => t.Id);
        base.OnModelCreating(modelBuilder);
    }
}