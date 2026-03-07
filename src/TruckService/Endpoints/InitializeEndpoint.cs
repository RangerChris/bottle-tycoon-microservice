using FastEndpoints;
using TruckService.Services;

namespace TruckService.Endpoints;

public class InitializeEndpoint(ITruckService truckService, ITruckTelemetryStore telemetryStore) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/initialize");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await truckService.ResetAsync();
        telemetryStore.RemoveAll();
        var truck = await truckService.CreateTruckAsync();
        telemetryStore.MarkActive(truck.Id, truck.Model);
        telemetryStore.Set(truck.Id, truck.Model, 0, 0, "idle");
    }
}