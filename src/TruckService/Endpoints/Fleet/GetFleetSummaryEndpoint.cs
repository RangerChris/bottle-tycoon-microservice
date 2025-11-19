using FastEndpoints;
using TruckService.Models;
using TruckService.Services;

namespace TruckService.Endpoints.Fleet;

public class GetFleetSummaryEndpoint : EndpointWithoutRequest<IEnumerable<TruckStatusDto>>
{
    private readonly ITruckManager _manager;

    public GetFleetSummaryEndpoint(ITruckManager manager)
    {
        _manager = manager;
    }

    public override void Configure()
    {
        Get("/api/v1/trucks/fleet/summary");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var fleet = await _manager.GetFleetSummaryAsync(ct);
        await Send.OkAsync(fleet, ct);
    }
}