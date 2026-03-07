using FastEndpoints;
using TruckService.Models;
using TruckService.Services;

namespace TruckService.Endpoints.ListTrucks;

public class ListTrucksEndpoint(ITruckRepository repo, ITruckTelemetryStore telemetryStore) : EndpointWithoutRequest<IEnumerable<TruckDto>>
{
    public override void Configure()
    {
        Get("/truck");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var trucks = (await repo.GetAllAsync(ct)).ToList();
        foreach (var truck in trucks)
        {
            telemetryStore.MarkActive(truck.Id, truck.Model);
        }

        await Send.OkAsync(trucks, ct);
    }
}