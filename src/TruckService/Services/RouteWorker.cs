using Microsoft.EntityFrameworkCore;
using TruckService.Data;

namespace TruckService.Services;

public class RouteWorker(TruckDbContext db, ILogger<RouteWorker> logger, TruckMetrics metrics, ITruckTelemetryStore telemetryStore)
    : IRouteWorker
{
    // Advances queued deliveries through the simple state machine synchronously for tests
    public async Task RunOnceAsync(CancellationToken ct = default)
    {
        // pick one queued delivery (avoid ordering by DateTimeOffset - SQLite translation limitation)
        var delivery = await db.Deliveries.Where(d => d.State == "Queued").FirstOrDefaultAsync(ct);
        if (delivery is null)
        {
            return;
        }

        // Move through states: Queued -> AtRecycler -> Loaded -> AtPlant -> Completed
        delivery.State = "AtRecycler";
        await db.SaveChangesAsync(ct);

        delivery.State = "Loaded";
        await db.SaveChangesAsync(ct);

        // Publish TruckLoaded event
        delivery.GetLoadByType();
        delivery.State = "AtPlant";
        await db.SaveChangesAsync(ct);

        delivery.State = "Completed";
        delivery.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        metrics.RecordDeliveryCompleted();

        var truck = await db.Trucks.FindAsync([delivery.TruckId], ct);
        if (truck != null)
        {
            truck.TotalEarnings += delivery.NetProfit;
            truck.SetCurrentLoadByType(new Dictionary<string, int>());
            await db.SaveChangesAsync(ct);

            var capacityUnits = Math.Floor(100 * Math.Pow(1.25, truck.CapacityLevel));
            telemetryStore.Set(truck.Id, truck.Model, 0, (int)capacityUnits, "idle");
        }

        logger.LogInformation("Processed delivery {DeliveryId} to Completed", delivery.Id);
    }
}