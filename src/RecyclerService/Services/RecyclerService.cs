using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using RecyclerService.Data;
using RecyclerService.Models;

namespace RecyclerService.Services;

public class RecyclerService : IRecyclerService
{
    private readonly RecyclerDbContext _db;
    private readonly ILogger<RecyclerService> _logger;
    private readonly Counter<long> _bottlesProcessed;

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
        return await _db.Recyclers.Include(r => r.Customers).FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<List<Recycler>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Recyclers.Include(r => r.Customers).ToListAsync(ct);
    }

    public async Task<Recycler> CustomerArrivedAsync(Guid recyclerId, Customer customer, CancellationToken ct = default)
    {
        var recycler = await _db.Recyclers.FirstOrDefaultAsync(r => r.Id == recyclerId, ct);
        if (recycler == null)
        {
            throw new KeyNotFoundException($"Recycler {recyclerId} not found");
        }

        customer.Id = customer.Id == Guid.Empty ? Guid.NewGuid() : customer.Id;
        customer.RecyclerId = recyclerId;
        customer.ArrivedAt = customer.ArrivedAt == default ? DateTimeOffset.UtcNow : customer.ArrivedAt;

        _db.Customers.Add(customer);

        var recyclerInventory = recycler.GetBottleInventory();
        var customerCounts = customer.GetBottleCounts();

        _logger.LogInformation("Customer arrived with bottles - Glass: {Glass}, Metal: {Metal}, Plastic: {Plastic}",
            customerCounts.GetValueOrDefault("glass"),
            customerCounts.GetValueOrDefault("metal"),
            customerCounts.GetValueOrDefault("plastic"));

        foreach (var kv in customerCounts)
        {
            recyclerInventory[kv.Key] = recyclerInventory.GetValueOrDefault(kv.Key) + kv.Value;

            if (kv.Value > 0 && !string.IsNullOrWhiteSpace(kv.Key))
            {
                _bottlesProcessed.Add(kv.Value, new KeyValuePair<string, object?>("bottle_type", kv.Key));
            }
        }

        recycler.SetBottleInventory(recyclerInventory);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Customer {CustomerId} arrived at Recycler {RecyclerId}, new load {CurrentLoad}/{Capacity}", customer.Id, recyclerId, recycler.CurrentLoad, recycler.Capacity);

        if (recycler.CurrentLoad >= recycler.Capacity)
        {
            _logger.LogInformation("Recycler {RecyclerId} reached capacity", recyclerId);
        }

        return recycler;
    }

    public async Task ResetAsync()
    {
        _db.Customers.RemoveRange(_db.Customers);
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