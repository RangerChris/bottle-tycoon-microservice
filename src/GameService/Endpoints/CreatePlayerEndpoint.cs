using FastEndpoints;
using GameService.Models;
using GameService.Services;

namespace GameService.Endpoints;

public class CreatePlayerEndpoint(IPlayerService playerService) : Endpoint<Player, Player>
{
    public override void Configure()
    {
        Post("/player");
        AllowAnonymous();
    }

    public override async Task HandleAsync(Player req, CancellationToken ct)
    {
        var player = await playerService.CreatePlayerAsync(req);
        var location = $"/player/{player.Id}";
        await Send.ResultAsync(TypedResults.Created(location, player));
    }
}