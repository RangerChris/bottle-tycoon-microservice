using System.Diagnostics.Metrics;

namespace TruckService.Services;

public sealed class TruckMetrics
{
    private readonly ObservableGauge<int> _capacity;
    private readonly ObservableGauge<int> _currentLoad;
    private readonly ObservableGauge<int> _active;
    private readonly Counter<long> _deliveriesCompleted;
    private readonly ITruckTelemetryStore _telemetryStore;

    public TruckMetrics(Meter meter, ITruckTelemetryStore telemetryStore)
    {
        _telemetryStore = telemetryStore;

        _currentLoad = meter.CreateObservableGauge(
            "truck_current_load",
            () => GetActiveSnapshots().Select(snapshot =>
                new Measurement<int>(snapshot.CurrentLoad,
                    new KeyValuePair<string, object?>("truck_id", snapshot.TruckId.ToString()),
                    new KeyValuePair<string, object?>("truck_name", snapshot.TruckName))),
            "bottles",
            "Current bottle load per truck");

        _capacity = meter.CreateObservableGauge(
            "truck_capacity",
            () => GetActiveSnapshots().Select(snapshot =>
                new Measurement<int>(snapshot.Capacity,
                    new KeyValuePair<string, object?>("truck_id", snapshot.TruckId.ToString()),
                    new KeyValuePair<string, object?>("truck_name", snapshot.TruckName))),
            "bottles",
            "Truck capacity per truck");

        _active = meter.CreateObservableGauge(
            "truck_active",
            () => _telemetryStore.GetAll().Select(snapshot =>
                new Measurement<int>(snapshot.IsActive ? 1 : 0,
                    new KeyValuePair<string, object?>("truck_id", snapshot.TruckId.ToString()),
                    new KeyValuePair<string, object?>("truck_name", snapshot.TruckName))),
            "state",
            "Truck active lifecycle state (1=active, 0=inactive)");

        _deliveriesCompleted = meter.CreateCounter<long>(
            "truck_deliveries",
            "deliveries",
            "Number of deliveries completed by truck");
    }

    private IEnumerable<TruckTelemetrySnapshot> GetActiveSnapshots()
    {
        return _telemetryStore.GetAll().Where(snapshot => snapshot.IsActive);
    }

    public void RecordDeliveryCompleted()
    {
        _deliveriesCompleted.Add(1);
    }
}