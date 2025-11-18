using FastEndpoints;
using TruckService.Models;
using TruckService.Services;

namespace TruckService.Endpoints.GetTruck;

public class GetTruckEndpoint : Endpoint<GetTruckRequest, TruckDto>
{
    private readonly ITruckRepository _repo;

    public GetTruckEndpoint(ITruckRepository repo)
    {
        _repo = repo;
    }

    public override void Configure()
    {
        Get("/truck/{id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetTruckRequest req, CancellationToken ct)
    {
        var truck = await _repo.GetByIdAsync(req.Id, ct);
        if (truck is null)
        {
            await Send.NotFoundAsync(ct);
        }
        else
        {
            await Send.OkAsync(truck);
        }
    }
}