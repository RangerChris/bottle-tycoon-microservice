using Microsoft.EntityFrameworkCore;
using TruckService.Data;

namespace TruckService.Services;

public class RouteWorker : IRouteWorker
{
    private readonly TruckDbContext _db;
    private readonly ILogger<RouteWorker> _logger;
    private readonly TruckMetrics _metrics;
    private readonly ITruckTelemetryStore _telemetryStore;

    public RouteWorker(TruckDbContext db, ILogger<RouteWorker> logger, TruckMetrics metrics, ITruckTelemetryStore telemetryStore)
    {
        _db = db;
        _logger = logger;
        _metrics = metrics;
        _telemetryStore = telemetryStore;
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

        delivery.State = "AtPlant";
        await _db.SaveChangesAsync(ct);

        delivery.State = "Completed";
        delivery.CompletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        _metrics.RecordDeliveryCompleted();

        var truck = await _db.Trucks.FindAsync([delivery.TruckId], ct);
        if (truck != null)
        {
            truck.TotalEarnings += delivery.NetProfit;
            truck.SetCurrentLoadByType(new Dictionary<string, int>());
            await _db.SaveChangesAsync(ct);

            var capacityUnits = Math.Floor(100 * Math.Pow(1.25, truck.CapacityLevel));
            _telemetryStore.Set(truck.Id, truck.Model, 0, (int)capacityUnits, "idle");
        }

        _logger.LogInformation("Processed delivery {DeliveryId} to Completed", delivery.Id);
    }
}