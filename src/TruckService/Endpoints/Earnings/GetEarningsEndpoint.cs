using FastEndpoints;
using TruckService.Services;

namespace TruckService.Endpoints.Earnings;

public class GetEarningsEndpoint(ITruckManager manager) : Endpoint<GetEarningsRequest, decimal>
{
    public override void Configure()
    {
        Get("/api/v1/truck/{TruckId}/earnings");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetEarningsRequest req, CancellationToken ct)
    {
        var earnings = await manager.GetEarningsAsync(req.TruckId, ct);
        await Send.OkAsync(earnings, ct);
    }
}