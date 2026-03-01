using FastEndpoints;
using GameService.Models;
using GameService.Services;

namespace GameService.Endpoints;

public class GetAllPlayersEndpoint(IPlayerService playerService) : EndpointWithoutRequest<List<Player>>
{
    public override void Configure()
    {
        Get("/player");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var players = await playerService.GetAllPlayersAsync();
        await Send.ResultAsync(TypedResults.Ok(players));
    }
}