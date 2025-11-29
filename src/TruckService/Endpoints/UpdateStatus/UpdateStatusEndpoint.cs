using FastEndpoints;
using TruckService.Services;

namespace TruckService.Endpoints.UpdateStatus;

public class UpdateStatusEndpoint : Endpoint<UpdateStatusRequest>
{
    private readonly ITruckRepository _repo;

    public UpdateStatusEndpoint(ITruckRepository repo)
    {
        _repo = repo;
    }

    public override void Configure()
    {
        Patch("/truck/{TruckId}/status");
        AllowAnonymous();
    }

    public override async Task HandleAsync(UpdateStatusRequest req, CancellationToken ct)
    {
        var idStr = Route<string>("TruckId");
        if (string.IsNullOrEmpty(idStr) || !Guid.TryParse(idStr, out var id))
        {
            await Send.ResultAsync(TypedResults.BadRequest());
            return;
        }

        var truck = await _repo.GetByIdAsync(id, ct);
        if (truck is null)
        {
            await Send.ResultAsync(TypedResults.NotFound());
            return;
        }

        truck.IsActive = req.IsActive;
        await _repo.UpdateAsync(truck, ct);
        await Send.ResultAsync(TypedResults.NoContent());
    }
}