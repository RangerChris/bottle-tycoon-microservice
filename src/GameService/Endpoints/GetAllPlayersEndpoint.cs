using FastEndpoints;
using GameService.Models;
using GameService.Services;

namespace GameService.Endpoints;

public class GetAllPlayersEndpoint : EndpointWithoutRequest<List<Player>>
{
    private readonly IPlayerService _playerService;

    public GetAllPlayersEndpoint(IPlayerService playerService)
    {
        _playerService = playerService;
    }

    public override void Configure()
    {
        Get("/player");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var players = await _playerService.GetAllPlayersAsync();
        await Send.ResultAsync(TypedResults.Ok(players));
    }
}