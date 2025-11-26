using MassTransit;
using Microsoft.EntityFrameworkCore;
using RecyclerService.Data;
using RecyclerService.Events;
using RecyclerService.Models;

namespace RecyclerService.Services;

public class RecyclerService : IRecyclerService
{
    private readonly RecyclerDbContext _db;
    private readonly ILogger<RecyclerService> _logger;
    private readonly IPublishEndpoint _publisher;

    public RecyclerService(RecyclerDbContext db, IPublishEndpoint publisher, ILogger<RecyclerService> logger)
    {
        _db = db;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<Recycler?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Recyclers.Include(r => r.Visitors).FirstOrDefaultAsync(r => r.Id == id, ct);
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
        }

        recycler.SetBottleInventory(recyclerInventory);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Visitor {VisitorId} arrived at Recycler {RecyclerId}, new load {CurrentLoad}/{Capacity}", visitor.Id, recyclerId, recycler.CurrentLoad, recycler.Capacity);

        if (recycler.CurrentLoad >= recycler.Capacity)
        {
            _logger.LogInformation("Recycler {RecyclerId} reached capacity, publishing RecyclerFull event", recyclerId);
            await _publisher.Publish(new RecyclerFull(recycler.Id, recycler.Capacity, recycler.CurrentLoad, DateTimeOffset.UtcNow), ct);
        }

        return recycler;
    }
}