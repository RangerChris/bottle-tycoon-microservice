using Microsoft.EntityFrameworkCore;

namespace TruckService.Data;

public class TruckDbContext : DbContext
{
    public TruckDbContext(DbContextOptions<TruckDbContext> opts) : base(opts)
    {
    }

    public DbSet<TruckEntity> Trucks { get; set; } = null!;
    public DbSet<DeliveryEntity> Deliveries { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TruckEntity>().HasKey(t => t.Id);
        modelBuilder.Entity<DeliveryEntity>().HasKey(d => d.Id);
        modelBuilder.Entity<DeliveryEntity>().Property(d => d.Timestamp).IsRequired();
        base.OnModelCreating(modelBuilder);
    }
}