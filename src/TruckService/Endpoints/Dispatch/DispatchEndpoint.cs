using FastEndpoints;
using TruckService.Services;

namespace TruckService.Endpoints.Dispatch;

public class DispatchEndpoint : Endpoint<DispatchRequest, bool>
{
    private readonly ITruckManager _manager;

    public DispatchEndpoint(ITruckManager manager)
    {
        _manager = manager;
    }

    public override void Configure()
    {
        Post("/api/v1/truck/{TruckId}/dispatch");
        AllowAnonymous();
    }

    public override async Task HandleAsync(DispatchRequest req, CancellationToken ct)
    {
        var ok = await _manager.DispatchAsync(req.TruckId, req.RecyclerId, req.DistanceKm, ct);
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