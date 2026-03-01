using FastEndpoints;
using RecyclingPlantService.Data;
using RecyclingPlantService.Services;

namespace RecyclingPlantService.Endpoints;

public class GetPlayerEarningsEndpoint(IRecyclingPlantService service) : EndpointWithoutRequest<PlayerEarnings>
{
    public override void Configure()
    {
        Get("/api/v1/recycling-plant/players/{PlayerId}/earnings");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var playerId = Route<Guid>("PlayerId");
        var earnings = await service.GetPlayerEarningsAsync(playerId);
        await Send.OkAsync(earnings);
    }
}