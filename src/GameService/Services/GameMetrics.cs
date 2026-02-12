using System.Diagnostics.Metrics;

namespace GameService.Services;

public sealed class GameMetrics
{
    private static readonly Meter Meter = new("GameService");

    public GameMetrics(IGameTelemetryStore telemetryStore)
    {
        var telemetryStore1 = telemetryStore;

        Meter.CreateObservableGauge(
            "player_total_earnings",
            () => telemetryStore1.GetAll().Select(snapshot =>
                new Measurement<double>((double)snapshot.TotalEarnings,
                    new KeyValuePair<string, object?>("player_id", snapshot.PlayerId.ToString()))),
            "credits",
            "Total earnings per player");
    }
}