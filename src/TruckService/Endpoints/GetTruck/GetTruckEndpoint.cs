using FastEndpoints;
using TruckService.Models;
using TruckService.Services;

namespace TruckService.Endpoints.GetTruck;

public class GetTruckEndpoint(ITruckRepository repo) : Endpoint<GetTruckRequest, TruckDto>
{
    public override void Configure()
    {
        Get("/truck/{TruckId}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetTruckRequest req, CancellationToken ct)
    {
        var truck = await repo.GetByIdAsync(req.TruckId, ct);
        if (truck is null)
        {
            await Send.NotFoundAsync(ct);
        }
        else
        {
            await Send.OkAsync(truck, ct);
        }
    }
}