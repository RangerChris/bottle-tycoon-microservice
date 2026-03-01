using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using TruckService.Data;

namespace TruckService.Services;

public sealed class TruckMetrics
{
    private readonly ObservableGauge<int> _capacity;
    private readonly ObservableGauge<int> _currentLoad;
    private readonly Counter<long> _deliveriesCompleted;
    private readonly IServiceProvider _serviceProvider;
    private readonly ITruckTelemetryStore _telemetryStore;

    public TruckMetrics(Meter meter, ITruckTelemetryStore telemetryStore, IServiceProvider serviceProvider)
    {
        _telemetryStore = telemetryStore;
        _serviceProvider = serviceProvider;

        _currentLoad = meter.CreateObservableGauge(
            "truck_current_load",
            () => GetValidSnapshots().Select(snapshot =>
                new Measurement<int>(snapshot.CurrentLoad,
                    new KeyValuePair<string, object?>("truck_id", snapshot.TruckId.ToString()),
                    new KeyValuePair<string, object?>("truck_name", snapshot.TruckName))),
            "bottles",
            "Current bottle load per truck");

        _capacity = meter.CreateObservableGauge(
            "truck_capacity",
            () => GetValidSnapshots().Select(snapshot =>
                new Measurement<int>(snapshot.Capacity,
                    new KeyValuePair<string, object?>("truck_id", snapshot.TruckId.ToString()),
                    new KeyValuePair<string, object?>("truck_name", snapshot.TruckName))),
            "bottles",
            "Truck capacity per truck");

        _deliveriesCompleted = meter.CreateCounter<long>(
            "truck_deliveries",
            "deliveries",
            "Number of deliveries completed by truck");
    }

    private IEnumerable<TruckTelemetrySnapshot> GetValidSnapshots()
    {
        var allSnapshots = _telemetryStore.GetAll();

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TruckDbContext>();
        var validTruckIds = db.Trucks.AsNoTracking().Select(t => t.Id).ToList();

        var validSnapshots = allSnapshots.Where(s => validTruckIds.Contains(s.TruckId)).ToList();

        return validSnapshots;
    }

    public void RecordDeliveryCompleted()
    {
        _deliveriesCompleted.Add(1);
    }
}