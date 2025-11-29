using FastEndpoints;
using GameService.Models;
using GameService.Services;

namespace GameService.Endpoints;

public class GetPlayerEndpoint : EndpointWithoutRequest<Player>
{
    private readonly IPlayerService _playerService;

    public GetPlayerEndpoint(IPlayerService playerService)
    {
        _playerService = playerService;
    }

    public override void Configure()
    {
        Get("/player/{id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var playerId = Route<Guid>("id");
        var player = await _playerService.GetPlayerAsync(playerId);

        if (player == null)
        {
            ThrowError("Player not found", 404);
            return;
        }

        await Send.ResultAsync(TypedResults.Ok(player));
    }
}