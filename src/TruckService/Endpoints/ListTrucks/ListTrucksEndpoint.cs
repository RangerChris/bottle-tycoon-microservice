using FastEndpoints;
using TruckService.Models;
using TruckService.Services;

namespace TruckService.Endpoints.ListTrucks;

public class ListTrucksEndpoint(ITruckRepository repo) : EndpointWithoutRequest<IEnumerable<TruckDto>>
{
    public override void Configure()
    {
        Get("/truck");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var trucks = await repo.GetAllAsync(ct);
        await Send.OkAsync(trucks);
    }
}