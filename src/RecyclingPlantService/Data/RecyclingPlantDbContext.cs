using Microsoft.EntityFrameworkCore;

namespace RecyclingPlantService.Data;

public class RecyclingPlantDbContext(DbContextOptions<RecyclingPlantDbContext> options) : DbContext(options)
{
    public DbSet<PlantDelivery> PlantDeliveries { get; set; }
    public DbSet<PlayerEarnings> PlayerEarnings { get; set; }
}