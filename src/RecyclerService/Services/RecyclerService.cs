using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using RecyclerService.Data;
using RecyclerService.Models;

namespace RecyclerService.Services;

public class RecyclerService(RecyclerDbContext db, ILogger<RecyclerService> logger, Counter<long> bottlesProcessed, ICustomerQueueService queueService)
    : IRecyclerService
{
    public async Task<Recycler?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await db.Recyclers.Include(r => r.Customers).FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<List<Recycler>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.Recyclers.Include(r => r.Customers).ToListAsync(ct);
    }

    public async Task<Recycler> CustomerArrivedAsync(Guid recyclerId, Customer customer, CancellationToken ct = default)
    {
        var recycler = await GetByIdAsync(recyclerId, ct);
        if (recycler is null)
        {
            throw new KeyNotFoundException($"Recycler {recyclerId} not found");
        }

        if (recycler.IsBlockedForSale)
        {
            throw new InvalidOperationException("Cannot accept customers - recycler is being sold");
        }

        var customerCounts = customer.GetBottleCounts();
        var recyclerInventory = recycler.GetBottleInventory();

        logger.LogInformation("Customer {CustomerId} arrived at Recycler {RecyclerId} with Glass={Glass}, Metal={Metal}, Plastic={Plastic}",
            customer.Id, recyclerId, customerCounts.GetValueOrDefault("glass"), customerCounts.GetValueOrDefault("metal"), customerCounts.GetValueOrDefault("plastic"));

        foreach (var kv in customerCounts)
        {
            recyclerInventory[kv.Key] = recyclerInventory.GetValueOrDefault(kv.Key) + kv.Value;

            if (kv.Value > 0 && !string.IsNullOrWhiteSpace(kv.Key))
            {
                bottlesProcessed.Add(kv.Value, new KeyValuePair<string, object?>("bottle_type", kv.Key));
            }
        }

        recycler.SetBottleInventory(recyclerInventory);

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Customer {CustomerId} arrived at Recycler {RecyclerId}, new load {CurrentLoad}/{Capacity}", customer.Id, recyclerId, recycler.CurrentLoad, recycler.Capacity);


        return recycler;
    }

    public async Task ResetAsync()
    {
        db.Customers.RemoveRange(db.Customers);
        db.Recyclers.RemoveRange(db.Recyclers);
        await db.SaveChangesAsync();
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
        if (string.IsNullOrEmpty(r.Name))
        {
            var existingCount = await db.Recyclers.CountAsync();
            r.Name = $"Recycler {existingCount + 1}";
        }

        db.Recyclers.Add(r);
        await db.SaveChangesAsync();
        return r;
    }

    public Task RecordBottlesProcessedAsync(Dictionary<string, int> bottlesByType)
    {
        foreach (var kv in bottlesByType)
        {
            if (kv.Value > 0 && !string.IsNullOrWhiteSpace(kv.Key))
            {
                bottlesProcessed.Add(kv.Value, new KeyValuePair<string, object?>("bottle_type", kv.Key));
            }
        }

        return Task.CompletedTask;
    }

    public async Task<Customer?> GetNextCustomerAsync(Guid recyclerId, CancellationToken ct = default)
    {
        return await queueService.GetNextWaitingCustomerAsync(recyclerId, ct);
    }

    public async Task MarkCustomerDoneAsync(Guid customerId, CancellationToken ct = default)
    {
        await queueService.MarkAsDoneAsync(customerId, ct);
    }

    public async Task<int> GetQueueDepthAsync(Guid recyclerId, CancellationToken ct = default)
    {
        return await queueService.GetQueueDepthAsync(recyclerId, ct);
    }
}