using Microsoft.EntityFrameworkCore;

namespace RecyclingPlantService.Data;

public class RecyclingPlantDbContext : DbContext
{
    public RecyclingPlantDbContext(DbContextOptions<RecyclingPlantDbContext> options) : base(options)
    {
    }

    // Add DbSets here if needed, e.g., for delivery logs
    public DbSet<PlantDelivery> PlantDeliveries { get; set; }
    public DbSet<PlayerEarnings> PlayerEarnings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Configure entities here
    }
}