using FastEndpoints;
using TruckService.Services;

namespace TruckService.Endpoints.Dispatch;

public class DispatchEndpoint(ITruckManager manager) : Endpoint<DispatchRequest, bool>
{
    public override void Configure()
    {
        Post("/api/v1/truck/{TruckId}/dispatch");
        AllowAnonymous();
    }

    public override async Task HandleAsync(DispatchRequest req, CancellationToken ct)
    {
        var ok = await manager.DispatchAsync(req.TruckId, req.RecyclerId, req.DistanceKm, ct);
        if (!ok)
        {
            await Send.NotFoundAsync(ct);
        }
        else
        {
            await Send.OkAsync(true, ct);
        }
    }
}