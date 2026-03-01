using FastEndpoints;
using TruckService.Services;

namespace TruckService.Endpoints.UpdateStatus;

public class UpdateStatusEndpoint(ITruckRepository repo) : Endpoint<UpdateStatusRequest>
{
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

        var truck = await repo.GetByIdAsync(id, ct);
        if (truck is null)
        {
            await Send.ResultAsync(TypedResults.NotFound());
            return;
        }

        truck.IsActive = req.IsActive;
        await repo.UpdateAsync(truck, ct);
        await Send.ResultAsync(TypedResults.NoContent());
    }
}