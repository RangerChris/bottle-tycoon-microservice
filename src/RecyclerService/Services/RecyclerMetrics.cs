using System.Diagnostics.Metrics;

namespace RecyclerService.Services;

public sealed class RecyclerMetrics
{
    private readonly ObservableGauge<int> _currentBottles;
    private readonly ObservableGauge<int> _currentVisitors;
    private readonly ObservableGauge<int> _queueDepth;
    private readonly ObservableGauge<int> _active;
    private readonly IRecyclerTelemetryStore _telemetryStore;

    public RecyclerMetrics(Meter meter, IRecyclerTelemetryStore telemetryStore)
    {
        _telemetryStore = telemetryStore;

        _currentBottles = meter.CreateObservableGauge(
            "recycler_current_bottles",
            () => GetActiveSnapshots().Select(snapshot =>
                new Measurement<int>(snapshot.CurrentBottles,
                    new KeyValuePair<string, object?>("recycler_id", snapshot.RecyclerId.ToString()),
                    new KeyValuePair<string, object?>("recycler_name", snapshot.RecyclerName))),
            "bottles",
            "Current bottles per recycler");

        _currentVisitors = meter.CreateObservableGauge(
            "recycler_current_visitors",
            () => GetActiveSnapshots().Select(snapshot =>
                new Measurement<int>(snapshot.CurrentVisitors,
                    new KeyValuePair<string, object?>("recycler_id", snapshot.RecyclerId.ToString()),
                    new KeyValuePair<string, object?>("recycler_name", snapshot.RecyclerName))),
            "visitors",
            "Current visitors at recycler (including waiting)");

        _queueDepth = meter.CreateObservableGauge(
            "recycler_queue_depth",
            () => GetActiveSnapshots().Select(snapshot =>
                new Measurement<int>(snapshot.QueueDepth,
                    new KeyValuePair<string, object?>("recycler_id", snapshot.RecyclerId.ToString()),
                    new KeyValuePair<string, object?>("recycler_name", snapshot.RecyclerName))),
            "customers",
            "Number of customers waiting in queue at recycler");

        _active = meter.CreateObservableGauge(
            "recycler_active",
            () => _telemetryStore.GetAll().Select(snapshot =>
                new Measurement<int>(snapshot.IsActive ? 1 : 0,
                    new KeyValuePair<string, object?>("recycler_id", snapshot.RecyclerId.ToString()),
                    new KeyValuePair<string, object?>("recycler_name", snapshot.RecyclerName))),
            "state",
            "Recycler active lifecycle state (1=active, 0=inactive)");
    }

    private IEnumerable<RecyclerTelemetrySnapshot> GetActiveSnapshots()
    {
        return _telemetryStore.GetAll().Where(snapshot => snapshot.IsActive);
    }
}