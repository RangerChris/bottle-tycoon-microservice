using FastEndpoints;
using TruckService.Models;
using TruckService.Services;

namespace TruckService.Endpoints.Status;

public class GetTruckStatusEndpoint(ITruckManager manager) : Endpoint<GetTruckStatusRequest, TruckStatusDto>
{
    public override void Configure()
    {
        Get("/api/v1/truck/{TruckId}/status");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetTruckStatusRequest req, CancellationToken ct)
    {
        try
        {
            var status = await manager.GetStatusAsync(req.TruckId, ct);
            await Send.OkAsync(status, ct);
        }
        catch (KeyNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}