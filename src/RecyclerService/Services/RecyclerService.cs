﻿using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using RecyclerService.Data;
using RecyclerService.Models;

namespace RecyclerService.Services;

public class RecyclerService : IRecyclerService
{
    private readonly RecyclerDbContext _db;
    private readonly ILogger<RecyclerService> _logger;
    private readonly Counter<long> _bottlesProcessed;

    // Single constructor that accepts an optional Meter. If not supplied by DI, create one locally.
    public RecyclerService(RecyclerDbContext db, ILogger<RecyclerService> logger, Meter? meter = null)
    {
        _db = db;
        _logger = logger;
        meter ??= new Meter("RecyclerService", "1.0");
        _bottlesProcessed = meter.CreateCounter<long>(
            "bottles_processed",
            unit: "bottles",
            description: "Number of bottles processed by type");
    }

    public async Task<Recycler?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Recyclers.Include(r => r.Visitors).FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<List<Recycler>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Recyclers.Include(r => r.Visitors).ToListAsync(ct);
    }

    public async Task<Recycler> VisitorArrivedAsync(Guid recyclerId, Visitor visitor, CancellationToken ct = default)
    {
        var recycler = await _db.Recyclers.FirstOrDefaultAsync(r => r.Id == recyclerId, ct);
        if (recycler == null)
        {
            throw new KeyNotFoundException($"Recycler {recyclerId} not found");
        }

        // Add visitor record
        visitor.Id = visitor.Id == Guid.Empty ? Guid.NewGuid() : visitor.Id;
        visitor.RecyclerId = recyclerId;
        visitor.ArrivedAt = visitor.ArrivedAt == default ? DateTimeOffset.UtcNow : visitor.ArrivedAt;

        _db.Visitors.Add(visitor);

        // Increase current load by the number of bottles by type
        var recyclerInventory = recycler.GetBottleInventory();
        var visitorCounts = visitor.GetBottleCounts();
        foreach (var kv in visitorCounts)
        {
            recyclerInventory[kv.Key] = recyclerInventory.GetValueOrDefault(kv.Key) + kv.Value;

            if (kv.Value > 0 && !string.IsNullOrWhiteSpace(kv.Key))
            {
                _bottlesProcessed.Add(kv.Value, new KeyValuePair<string, object?>("bottle_type", kv.Key));
            }
        }

        recycler.SetBottleInventory(recyclerInventory);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Visitor {VisitorId} arrived at Recycler {RecyclerId}, new load {CurrentLoad}/{Capacity}", visitor.Id, recyclerId, recycler.CurrentLoad, recycler.Capacity);

        if (recycler.CurrentLoad >= recycler.Capacity)
        {
            _logger.LogInformation("Recycler {RecyclerId} reached capacity, publishing RecyclerFull event", recyclerId);
        }

        return recycler;
    }

    public async Task ResetAsync()
    {
        _db.Recyclers.RemoveRange(_db.Recyclers);
        await _db.SaveChangesAsync();
    }

    public async Task<Recycler> CreateRecyclerAsync(Recycler? recycler = null)
    {
        var r = recycler ?? new Recycler();
        if (r.Id == Guid.Empty)
        {
            r.Id = Guid.NewGuid();
        }

        r.CreatedAt = DateTimeOffset.UtcNow;
        r.Capacity = r.Capacity == 0 ? 100 : r.Capacity;
        r.Name = string.IsNullOrEmpty(r.Name) ? $"Recycler-{r.Id}" : r.Name;

        _db.Recyclers.Add(r);
        await _db.SaveChangesAsync();
        return r;
    }

    public Task RecordBottlesProcessedAsync(Dictionary<string, int> bottlesByType, CancellationToken ct = default)
    {
        foreach (var kv in bottlesByType)
        {
            if (kv.Value > 0 && !string.IsNullOrWhiteSpace(kv.Key))
            {
                _bottlesProcessed.Add(kv.Value, new KeyValuePair<string, object?>("bottle_type", kv.Key));
            }
        }
        return Task.CompletedTask;
    }
}