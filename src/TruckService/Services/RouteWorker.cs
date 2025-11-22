using MassTransit;
using Microsoft.EntityFrameworkCore;
using Shared.Events;
using TruckService.Data;

namespace TruckService.Services;

public class RouteWorker : IRouteWorker
{
    private readonly TruckDbContext _db;
    private readonly ILogger<RouteWorker> _logger;
    private readonly IPublishEndpoint _publishEndpoint;

    public RouteWorker(TruckDbContext db, ILogger<RouteWorker> logger, IPublishEndpoint publishEndpoint)
    {
        _db = db;
        _logger = logger;
        _publishEndpoint = publishEndpoint;
    }

    // Advances queued deliveries through the simple state machine synchronously for tests
    public async Task RunOnceAsync(CancellationToken ct = default)
    {
        // pick one queued delivery (avoid ordering by DateTimeOffset - SQLite translation limitation)
        var delivery = await _db.Deliveries.Where(d => d.State == "Queued").FirstOrDefaultAsync(ct);
        if (delivery is null)
        {
            return;
        }

        // Move through states: Queued -> AtRecycler -> Loaded -> AtPlant -> Completed
        delivery.State = "AtRecycler";
        await _db.SaveChangesAsync(ct);

        delivery.State = "Loaded";
        await _db.SaveChangesAsync(ct);

        // Publish TruckLoaded event
        var loadByType = delivery.GetLoadByType();
        var operatingCost = delivery.OperatingCost; // assuming cost is per delivery
        var truckLoaded = new TruckLoaded(
            delivery.TruckId,
            delivery.RecyclerId,
            Guid.Empty, // PlayerId not known here, perhaps need to add
            loadByType,
            operatingCost,
            DateTimeOffset.UtcNow
        );
        await _publishEndpoint.Publish(truckLoaded);

        delivery.State = "AtPlant";
        await _db.SaveChangesAsync(ct);

        delivery.State = "Completed";
        delivery.CompletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Update truck earnings and current load
        var truck = await _db.Trucks.FindAsync(new object[] { delivery.TruckId }, ct);
        if (truck != null)
        {
            truck.TotalEarnings += delivery.NetProfit;
            truck.SetCurrentLoadByType(new Dictionary<string, int>()); // empty after delivery
            await _db.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Processed delivery {DeliveryId} to Completed", delivery.Id);
    }
}