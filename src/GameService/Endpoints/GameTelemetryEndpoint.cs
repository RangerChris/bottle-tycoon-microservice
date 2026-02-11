using FastEndpoints;
using GameService.Services;

namespace GameService.Endpoints;

public sealed class GameTelemetryEndpoint : Endpoint<GameTelemetryEndpoint.Request, GameTelemetryEndpoint.Response>
{
    private readonly IGameTelemetryStore _telemetryStore;
    private readonly ILogger<GameTelemetryEndpoint> _logger;

    public GameTelemetryEndpoint(IGameTelemetryStore telemetryStore, ILogger<GameTelemetryEndpoint> logger)
    {
        _telemetryStore = telemetryStore;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/player/{id:guid}/telemetry");
        AllowAnonymous();
        Options(x => x.WithTags("Game"));
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var playerId = Route<Guid>("id");

        var totalEarnings = req.TotalEarnings ?? 0;

        _telemetryStore.SetTotalEarnings(playerId, totalEarnings);
        _logger.LogInformation("Telemetry updated for player {PlayerId} with total earnings {TotalEarnings}",
            playerId, totalEarnings);

        await Send.OkAsync(new Response { PlayerId = playerId, TotalEarnings = totalEarnings }, ct);
    }

    public sealed record Request
    {
        public decimal? TotalEarnings { get; init; }
    }

    public new sealed record Response
    {
        public Guid PlayerId { get; init; }
        public decimal TotalEarnings { get; init; }
    }
}