using Microsoft.EntityFrameworkCore;
using TruckService.Data;
using TruckService.Models;

namespace TruckService.Services;

public class EfTruckRepository : ITruckRepository
{
    private readonly TruckDbContext _db;

    public EfTruckRepository(TruckDbContext db)
    {
        _db = db;
    }

    public async Task<TruckDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var ent = await _db.Trucks.FindAsync(new object[] { id }, ct);
        if (ent is null)
        {
            return null;
        }

        return new TruckDto { Id = ent.Id, LicensePlate = ent.LicensePlate, Model = ent.Model, IsActive = ent.IsActive };
    }

    public async Task<IEnumerable<TruckDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Trucks.Select(ent => new TruckDto { Id = ent.Id, LicensePlate = ent.LicensePlate, Model = ent.Model, IsActive = ent.IsActive }).ToListAsync(ct);
    }

    public async Task<TruckDto> CreateAsync(TruckDto truck, CancellationToken ct = default)
    {
        var ent = new TruckEntity { Id = truck.Id == Guid.Empty ? Guid.NewGuid() : truck.Id, LicensePlate = truck.LicensePlate, Model = truck.Model, IsActive = truck.IsActive };
        _db.Trucks.Add(ent);
        await _db.SaveChangesAsync(ct);
        truck.Id = ent.Id;
        return truck;
    }

    public async Task<bool> UpdateAsync(TruckDto truck, CancellationToken ct = default)
    {
        var ent = await _db.Trucks.FindAsync(new object[] { truck.Id }, ct);
        if (ent is null)
        {
            return false;
        }

        ent.LicensePlate = truck.LicensePlate;
        ent.Model = truck.Model;
        ent.IsActive = truck.IsActive;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var ent = await _db.Trucks.FindAsync(new object[] { id }, ct);
        if (ent is null)
        {
            return false;
        }

        _db.Trucks.Remove(ent);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}