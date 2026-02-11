using System.Diagnostics.Metrics;

namespace TruckService.Services;

public sealed class TruckMetrics
{
    private readonly ObservableGauge<int> _capacity;
    private readonly ObservableGauge<int> _currentLoad;
    private readonly Counter<long> _deliveriesCompleted;

    public TruckMetrics(Meter meter, ITruckTelemetryStore telemetryStore)
    {
        _currentLoad = meter.CreateObservableGauge(
            "truck_current_load",
            () => telemetryStore.GetAll().Select(snapshot =>
                new Measurement<int>(snapshot.CurrentLoad,
                    new KeyValuePair<string, object?>("truck_name", snapshot.TruckName))),
            "bottles",
            "Current bottle load per truck");

        _capacity = meter.CreateObservableGauge(
            "truck_capacity",
            () => telemetryStore.GetAll().Select(snapshot =>
                new Measurement<int>(snapshot.Capacity,
                    new KeyValuePair<string, object?>("truck_name", snapshot.TruckName))),
            "bottles",
            "Truck capacity per truck");

        _deliveriesCompleted = meter.CreateCounter<long>(
            "truck_deliveries",
            "deliveries",
            "Number of deliveries completed by truck");
    }

    public void RecordDeliveryCompleted()
    {
        _deliveriesCompleted.Add(1);
    }
}