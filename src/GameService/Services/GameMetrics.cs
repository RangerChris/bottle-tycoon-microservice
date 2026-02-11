using System.Diagnostics.Metrics;

namespace GameService.Services;

public sealed class GameMetrics
{
    private static readonly Meter Meter = new("GameService");
    private static IGameTelemetryStore _telemetryStore;

    public GameMetrics(IGameTelemetryStore telemetryStore)
    {
        _telemetryStore = telemetryStore;

        Meter.CreateObservableGauge(
            "player_total_earnings",
            () => _telemetryStore.GetAll().Select(snapshot =>
                new Measurement<double>((double)snapshot.TotalEarnings,
                    new KeyValuePair<string, object?>("player_id", snapshot.PlayerId.ToString()))),
            "credits",
            "Total earnings per player");
    }
}