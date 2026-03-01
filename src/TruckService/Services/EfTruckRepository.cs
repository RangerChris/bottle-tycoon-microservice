using Microsoft.EntityFrameworkCore;
using TruckService.Data;
using TruckService.Models;

namespace TruckService.Services;

public class EfTruckRepository(TruckDbContext db) : ITruckRepository
{
    public async Task<TruckDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var ent = await db.Trucks.FindAsync([id], ct);
        if (ent is null)
        {
            return null;
        }

        return new TruckDto { Id = ent.Id, Model = ent.Model, IsActive = ent.IsActive, Level = ent.CapacityLevel };
    }

    public async Task<TruckEntity?> GetEntityByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await db.Trucks.FindAsync([id], ct);
    }

    public async Task<IEnumerable<TruckDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.Trucks
            .Where(t => !t.IsBlockedForSale)
            .Select(ent => new TruckDto { Id = ent.Id, Model = ent.Model, IsActive = ent.IsActive, Level = ent.CapacityLevel })
            .ToListAsync(ct);
    }

    public async Task<TruckDto> CreateAsync(TruckDto truck, CancellationToken ct = default)
    {
        var ent = new TruckEntity { Id = truck.Id == Guid.Empty ? Guid.NewGuid() : truck.Id, Model = truck.Model, IsActive = truck.IsActive };
        db.Trucks.Add(ent);
        await db.SaveChangesAsync(ct);
        truck.Id = ent.Id;
        return truck;
    }

    public async Task<bool> UpdateAsync(TruckDto truck, CancellationToken ct = default)
    {
        var ent = await db.Trucks.FindAsync([truck.Id], ct);
        if (ent is null)
        {
            return false;
        }

        ent.Model = truck.Model;
        ent.IsActive = truck.IsActive;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UpdateAsync(TruckEntity truck, CancellationToken ct = default)
    {
        db.Trucks.Update(truck);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var ent = await db.Trucks.FindAsync([id], ct);
        if (ent is null)
        {
            return false;
        }

        db.Trucks.Remove(ent);
        await db.SaveChangesAsync(ct);
        return true;
    }
}