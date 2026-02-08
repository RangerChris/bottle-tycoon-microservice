using System.Diagnostics.Metrics;

namespace RecyclerService.Services;

public sealed class RecyclerMetrics
{
    private readonly ObservableGauge<int> _currentBottles;

    public RecyclerMetrics(Meter meter, IRecyclerTelemetryStore telemetryStore)
    {
        _currentBottles = meter.CreateObservableGauge(
            "recycler_current_bottles",
            () => telemetryStore.GetAll().Select(snapshot =>
                new Measurement<int>(snapshot.CurrentBottles, new KeyValuePair<string, object?>("recycler_id", snapshot.RecyclerId.ToString()))),
            "bottles",
            "Current bottles per recycler");
    }
}