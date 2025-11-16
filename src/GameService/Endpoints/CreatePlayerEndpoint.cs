using FastEndpoints;
using GameService.Models;
using GameService.Services;
using Microsoft.AspNetCore.Http;

namespace GameService.Endpoints;

public class CreatePlayerEndpoint : EndpointWithoutRequest<Player>
{
    private readonly IPlayerService _playerService;

    public CreatePlayerEndpoint(IPlayerService playerService)
    {
        _playerService = playerService;
    }

    public override void Configure()
    {
        Post("/players");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var player = await _playerService.CreatePlayerAsync();
        var location = $"/players/{player.Id}";
        await Send.ResultAsync(TypedResults.Created(location, player));
    }
}