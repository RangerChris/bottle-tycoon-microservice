using FastEndpoints;
using HeadquartersService.Services;

namespace HeadquartersService.Endpoints;

public class GetFleetStatusEndpoint : EndpointWithoutRequest
{
    private readonly IFleetService _fleet;

    public GetFleetStatusEndpoint(IFleetService fleet)
    {
        _fleet = fleet;
    }

    public override void Configure()
    {
        Get("/api/v1/headquarters/fleet/status");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        var trucks = _fleet.GetAll();
        Send.OkAsync(trucks, ct);
        return Task.CompletedTask;
    }
}