using FastEndpoints;
using TruckService.Models;
using TruckService.Services;

namespace TruckService.Endpoints.CreateTruck;

public class CreateTruckEndpoint : Endpoint<CreateTruckRequest, TruckDto>
{
    private readonly ITruckRepository _repo;

    public CreateTruckEndpoint(ITruckRepository repo)
    {
        _repo = repo;
    }

    public override void Configure()
    {
        Post("/truck");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CreateTruckRequest req, CancellationToken ct)
    {
        var dto = new TruckDto { Id = req.Id, LicensePlate = req.LicensePlate, Model = req.Model, IsActive = req.IsActive };
        var created = await _repo.CreateAsync(dto, ct);
        await Send.ResultAsync(TypedResults.Created($"/truck/{created.Id}", created));
    }
}