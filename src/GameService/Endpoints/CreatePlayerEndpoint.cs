using FastEndpoints;
using GameService.Models;
using GameService.Services;

namespace GameService.Endpoints;

public class CreatePlayerEndpoint : Endpoint<Player, Player>
{
    private readonly IPlayerService _playerService;

    public CreatePlayerEndpoint(IPlayerService playerService)
    {
        _playerService = playerService;
    }

    public override void Configure()
    {
        Post("/player");
        AllowAnonymous();
    }

    public override async Task HandleAsync(Player req, CancellationToken ct)
    {
        var player = await _playerService.CreatePlayerAsync(req);
        var location = $"/player/{player.Id}";
        await Send.ResultAsync(TypedResults.Created(location, player));
    }
}