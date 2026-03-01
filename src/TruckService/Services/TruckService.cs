using Microsoft.EntityFrameworkCore;
using TruckService.Data;
using TruckService.Models;

namespace TruckService.Services;

public class TruckService(TruckDbContext db, ILogger<TruckService> logger) : ITruckService
{
    private readonly ILogger<TruckService> _logger = logger;

    public async Task ResetAsync()
    {
        await db.Trucks.ExecuteDeleteAsync();
        await db.Deliveries.ExecuteDeleteAsync();
    }

    public async Task<TruckDto> CreateTruckAsync(TruckDto? truck = null)
    {
        var t = truck ?? new TruckDto();
        if (t.Id == Guid.Empty)
        {
            t.Id = Guid.NewGuid();
        }

        if (string.IsNullOrEmpty(t.Model))
        {
            var existingCount = await db.Trucks.CountAsync();
            t.Model = $"Truck {existingCount + 1}";
        }

        t.IsActive = true;
        t.Level = 0;

        var ent = new TruckEntity
        {
            Id = t.Id,
            Model = t.Model,
            IsActive = t.IsActive,
            CapacityLevel = 0,
            CurrentLoadByTypeJson = "{}",
            TotalEarnings = 0
        };

        db.Trucks.Add(ent);
        await db.SaveChangesAsync();
        return t;
    }
}