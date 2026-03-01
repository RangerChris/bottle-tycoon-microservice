using FastEndpoints;
using HeadquartersService.Services;

namespace HeadquartersService.Endpoints;

public class GetFleetStatusEndpoint(IFleetService fleet) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/api/v1/headquarters/fleet/status");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        var trucks = fleet.GetAll();
        Send.OkAsync(trucks, ct);
        return Task.CompletedTask;
    }
}