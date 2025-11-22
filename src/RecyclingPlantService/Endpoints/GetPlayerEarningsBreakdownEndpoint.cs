using FastEndpoints;
using RecyclingPlantService.Data;
using RecyclingPlantService.Services;

namespace RecyclingPlantService.Endpoints;

public class GetPlayerEarningsBreakdownEndpoint : EndpointWithoutRequest<EarningsBreakdown>
{
    private readonly IRecyclingPlantService _service;

    public GetPlayerEarningsBreakdownEndpoint(IRecyclingPlantService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/api/v1/recycling-plant/players/{PlayerId}/earnings/breakdown");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var playerId = Route<Guid>("PlayerId");
        var breakdown = await _service.GetPlayerEarningsBreakdownAsync(playerId);
        await Send.OkAsync(breakdown);
    }
}