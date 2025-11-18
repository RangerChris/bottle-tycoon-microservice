using FastEndpoints;
using TruckService.Models;
using TruckService.Services;

namespace TruckService.Endpoints.ListTrucks;

public class ListTrucksEndpoint : EndpointWithoutRequest<IEnumerable<TruckDto>>
{
    private readonly ITruckRepository _repo;

    public ListTrucksEndpoint(ITruckRepository repo)
    {
        _repo = repo;
    }

    public override void Configure()
    {
        Verbs("GET");
        Routes("/trucks");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var trucks = await _repo.GetAllAsync(ct);
        await Send.OkAsync(trucks);
    }
}