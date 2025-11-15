using FastEndpoints;
using GameService.Models;
using GameService.Services;

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
        await SendCreatedAtAsync<GetPlayerEndpoint>(new { id = player.Id }, player, cancellation: ct);
    }
}