using System.Diagnostics.Metrics;

namespace RecyclerService.Services;

public sealed class RecyclerMetrics
{
    private readonly ObservableGauge<int> _currentBottles;
    private readonly ObservableGauge<int> _currentVisitors;

    public RecyclerMetrics(Meter meter, IRecyclerTelemetryStore telemetryStore)
    {
        _currentBottles = meter.CreateObservableGauge(
            "recycler_current_bottles",
            () => telemetryStore.GetAll().Select(snapshot =>
                new Measurement<int>(snapshot.CurrentBottles, new KeyValuePair<string, object?>("recycler_name", snapshot.RecyclerName))),
            "bottles",
            "Current bottles per recycler");

        _currentVisitors = meter.CreateObservableGauge(
            "recycler_current_visitors",
            () => telemetryStore.GetAll().Select(snapshot =>
                new Measurement<int>(snapshot.CurrentVisitors, new KeyValuePair<string, object?>("recycler_name", snapshot.RecyclerName))),
            "visitors",
            "Current visitors at recycler (including waiting)");
    }
}