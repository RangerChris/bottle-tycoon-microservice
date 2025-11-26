using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GameService.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<GameDbContext>
{
    public GameDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();

        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config.GetConnectionString("DefaultConnection")
                               ?? "Host=localhost;Database=GameServiceDb;Username=postgres;Password=password";

        var optionsBuilder = new DbContextOptionsBuilder<GameDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new GameDbContext(optionsBuilder.Options);
    }
}