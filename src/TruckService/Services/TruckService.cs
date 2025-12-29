using Microsoft.EntityFrameworkCore;
using TruckService.Data;
using TruckService.Models;

namespace TruckService.Services;

public class TruckService : ITruckService
{
    private readonly TruckDbContext _db;
    private readonly ILogger<TruckService> _logger;

    public TruckService(TruckDbContext db, ILogger<TruckService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task ResetAsync()
    {
        await _db.Trucks.ExecuteDeleteAsync();
        await _db.Deliveries.ExecuteDeleteAsync();
    }

    public async Task<TruckDto> CreateTruckAsync(TruckDto? truck = null)
    {
        var t = truck ?? new TruckDto();
        if (t.Id == Guid.Empty)
        {
            t.Id = Guid.NewGuid();
        }

        t.Model = string.IsNullOrEmpty(t.Model) ? "Standard Truck" : t.Model;
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

        _db.Trucks.Add(ent);
        await _db.SaveChangesAsync();
        return t;
    }
}