using FastEndpoints;
using GameService.Services;

namespace GameService.Endpoints;

public sealed class GameTelemetryEndpoint(IGameTelemetryStore telemetryStore, ILogger<GameTelemetryEndpoint> logger) : Endpoint<GameTelemetryEndpoint.Request, GameTelemetryEndpoint.Response>
{
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

        telemetryStore.SetTotalEarnings(playerId, totalEarnings);
        logger.LogInformation("Telemetry updated for player {PlayerId} with total earnings {TotalEarnings}",
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