using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using RecyclerService.Data;

namespace RecyclerService.Services;

public sealed class RecyclerMetrics
{
    private readonly ObservableGauge<int> _currentBottles;
    private readonly ObservableGauge<int> _currentVisitors;
    private readonly ObservableGauge<int> _queueDepth;
    private readonly IRecyclerTelemetryStore _telemetryStore;
    private readonly IServiceProvider _serviceProvider;

    public RecyclerMetrics(Meter meter, IRecyclerTelemetryStore telemetryStore, IServiceProvider serviceProvider)
    {
        _telemetryStore = telemetryStore;
        _serviceProvider = serviceProvider;

        _currentBottles = meter.CreateObservableGauge(
            "recycler_current_bottles",
            () => GetValidSnapshots().Select(snapshot =>
                new Measurement<int>(snapshot.CurrentBottles,
                    new KeyValuePair<string, object?>("recycler_id", snapshot.RecyclerId.ToString()),
                    new KeyValuePair<string, object?>("recycler_name", snapshot.RecyclerName))),
            "bottles",
            "Current bottles per recycler");

        _currentVisitors = meter.CreateObservableGauge(
            "recycler_current_visitors",
            () => GetValidSnapshots().Select(snapshot =>
                new Measurement<int>(snapshot.CurrentVisitors,
                    new KeyValuePair<string, object?>("recycler_id", snapshot.RecyclerId.ToString()),
                    new KeyValuePair<string, object?>("recycler_name", snapshot.RecyclerName))),
            "visitors",
            "Current visitors at recycler (including waiting)");

        _queueDepth = meter.CreateObservableGauge(
            "recycler_queue_depth",
            () => GetValidSnapshots().Select(snapshot =>
                new Measurement<int>(snapshot.QueueDepth,
                    new KeyValuePair<string, object?>("recycler_id", snapshot.RecyclerId.ToString()),
                    new KeyValuePair<string, object?>("recycler_name", snapshot.RecyclerName))),
            "customers",
            "Number of customers waiting in queue at recycler");
    }

    private IEnumerable<RecyclerTelemetrySnapshot> GetValidSnapshots()
    {
        var allSnapshots = _telemetryStore.GetAll();

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
        var validRecyclerIds = db.Recyclers.AsNoTracking().Select(r => r.Id).ToList();

        var validSnapshots = allSnapshots.Where(s => validRecyclerIds.Contains(s.RecyclerId)).ToList();

        return validSnapshots;
    }
}