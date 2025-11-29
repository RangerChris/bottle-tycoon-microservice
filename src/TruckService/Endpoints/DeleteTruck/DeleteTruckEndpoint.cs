using FastEndpoints;
using TruckService.Services;

namespace TruckService.Endpoints.DeleteTruck;

public class DeleteTruckEndpoint : EndpointWithoutRequest
{
    private readonly ITruckRepository _repo;

    public DeleteTruckEndpoint(ITruckRepository repo)
    {
        _repo = repo;
    }

    public override void Configure()
    {
        Delete("/truck/{TruckId}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var idStr = Route<string>("TruckId");
        if (string.IsNullOrEmpty(idStr) || !Guid.TryParse(idStr, out var id))
        {
            await Send.ResultAsync(TypedResults.BadRequest());
            return;
        }

        var ok = await _repo.DeleteAsync(id, ct);
        if (!ok)
        {
            await Send.ResultAsync(TypedResults.NotFound());
        }
        else
        {
            await Send.ResultAsync(TypedResults.NoContent());
        }
    }
}