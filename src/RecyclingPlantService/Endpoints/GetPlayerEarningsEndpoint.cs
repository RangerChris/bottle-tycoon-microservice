using FastEndpoints;
using RecyclingPlantService.Data;
using RecyclingPlantService.Services;

namespace RecyclingPlantService.Endpoints;

public class GetPlayerEarningsEndpoint : EndpointWithoutRequest<PlayerEarnings>
{
    private readonly IRecyclingPlantService _service;

    public GetPlayerEarningsEndpoint(IRecyclingPlantService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/api/v1/recycling-plant/players/{PlayerId}/earnings");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var playerId = Route<Guid>("PlayerId");
        var earnings = await _service.GetPlayerEarningsAsync(playerId);
        await Send.OkAsync(earnings);
    }
}