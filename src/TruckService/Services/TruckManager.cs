﻿using Microsoft.EntityFrameworkCore;
using TruckService.Data;
using TruckService.Models;

namespace TruckService.Services;

public class TruckManager : ITruckManager
{
    private readonly TruckDbContext _db;
    private readonly ILoadProvider _loadProvider;
    private readonly ILogger<TruckManager> _logger;
    private readonly ITruckRepository _repo;


    public TruckManager(ITruckRepository repo, TruckDbContext db, ILoadProvider loadProvider, ILogger<TruckManager> logger)
    {
        _repo = repo;
        _db = db;
        _loadProvider = loadProvider;
        _logger = logger;
    }

    public async Task<TruckStatusDto> GetStatusAsync(Guid truckId, CancellationToken ct = default)
    {
        var truck = await _db.Trucks.FindAsync(new object[] { truckId }, ct);
        if (truck == null)
        {
            throw new KeyNotFoundException($"Truck {truckId} not found");
        }

        // default values for now
        return new TruckStatusDto
        {
            Id = truck.Id,
            State = "Idle",
            Location = "Depot",
            CurrentLoadByType = truck.GetCurrentLoadByType(),
            MaxCapacityUnits = CalculateMaxCapacityUnits(100, 0),
            CapacityLevel = 0,
            TotalEarnings = await GetEarningsAsync(truckId, ct)
        };
    }

    public async Task<bool> DispatchAsync(Guid truckId, Guid recyclerId, double distanceKm, CancellationToken ct = default)
    {
        var truck = await _repo.GetByIdAsync(truckId, ct);
        if (truck == null)
        {
            return false;
        }

        _logger.LogInformation("Dispatching truck {TruckId} to recycler {RecyclerId} (distance {Distance} km)", truckId, recyclerId, distanceKm);
        // Use load provider to get deterministic load in tests
        var (glass, metal, plastic) = _loadProvider.GetLoadForRecycler(recyclerId);

        var loadUnits = CalculateCurrentLoadUnits(glass, metal, plastic);
        var maxUnits = CalculateMaxCapacityUnits(100, 0);
        if (loadUnits > maxUnits)
        {
            _logger.LogWarning("Load {Load} exceeds capacity {Max}, trimming", loadUnits, maxUnits);
            // trim proportionally
            var factor = maxUnits / loadUnits;
            glass = (int)Math.Floor(glass * factor);
            metal = (int)Math.Floor(metal * factor);
            plastic = (int)Math.Floor(plastic * factor);
        }

        // Travel to plant
        var gross = CalculateGrossEarnings(glass, metal, plastic);
        var costPerKm = CalculateCostPerKm(0);
        var operatingCost = (decimal)distanceKm * costPerKm;
        var net = gross - operatingCost;

        // persist delivery
        var delivery = new DeliveryEntity
        {
            Id = Guid.NewGuid(),
            TruckId = truckId,
            RecyclerId = recyclerId,
            Timestamp = DateTimeOffset.UtcNow,
            State = "Queued",
            LoadByTypeJson = DeliveryEntity.SerializeLoad(new Dictionary<string, int> { { "glass", glass }, { "metal", metal }, { "plastic", plastic } }),
            GrossEarnings = gross,
            OperatingCost = operatingCost,
            NetProfit = net
        };

        _db.Deliveries.Add(delivery);
        // persist truck current load by type
        var truckEnt = await _db.Trucks.FindAsync(new object[] { truckId }, ct);
        if (truckEnt != null)
        {
            truckEnt.SetCurrentLoadByType(new Dictionary<string, int> { { "glass", glass }, { "metal", metal }, { "plastic", plastic } });
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Delivery persisted: id {Id}, gross {Gross}, cost {Cost}, net {Net}", delivery.Id, gross, operatingCost, net);
        return true;
    }

    public async Task<IEnumerable<TruckStatusDto>> GetFleetSummaryAsync(CancellationToken ct = default)
    {
        var trucks = await _db.Trucks.ToListAsync(ct);
        return trucks.Select(t => new TruckStatusDto
        {
            Id = t.Id,
            State = t.IsActive ? "Idle" : "Inactive",
            Location = "Depot",
            CurrentLoadByType = t.GetCurrentLoadByType(),
            MaxCapacityUnits = CalculateMaxCapacityUnits(100, 0),
            CapacityLevel = 0,
            TotalEarnings = 0m
        });
    }

    public async Task<IEnumerable<object>> GetHistoryAsync(Guid truckId, CancellationToken ct = default)
    {
        var deliveries = await _db.Deliveries.Where(d => d.TruckId == truckId).OrderByDescending(d => d.Timestamp).ToListAsync(ct);
        return deliveries.Select(d => new
        {
            d.Id,
            d.TruckId,
            d.RecyclerId,
            d.Timestamp,
            LoadByType = d.GetLoadByType(),
            d.GrossEarnings,
            d.OperatingCost,
            d.NetProfit
        }).ToList();
    }

    public async Task<decimal> GetEarningsAsync(Guid truckId, CancellationToken ct = default)
    {
        var total = await _db.Deliveries.Where(d => d.TruckId == truckId).SumAsync(d => (decimal?)d.NetProfit, ct);
        return total ?? 0m;
    }

    private double CalculateCurrentLoadUnits(int glassCount, int metalCount, int plasticCount)
    {
        return glassCount * 2.0 + metalCount * 1.0 + plasticCount * 1.4;
    }

    private double CalculateMaxCapacityUnits(int baseCapacity, int capacityLevel)
    {
        return baseCapacity * Math.Pow(1.25, capacityLevel);
    }

    private decimal CalculateGrossEarnings(int glassCount, int metalCount, int plasticCount)
    {
        return (decimal)(glassCount * 4.0 + metalCount * 2.5 + plasticCount * 1.75);
    }

    private decimal CalculateCostPerKm(int capacityLevel)
    {
        return (decimal)(0.5 * Math.Pow(1.25, capacityLevel));
    }
}