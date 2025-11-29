using FastEndpoints;
using TruckService.Services;

namespace TruckService.Endpoints.Earnings;

public class GetEarningsEndpoint : Endpoint<GetEarningsRequest, decimal>
{
    private readonly ITruckManager _manager;

    public GetEarningsEndpoint(ITruckManager manager)
    {
        _manager = manager;
    }

    public override void Configure()
    {
        Get("/api/v1/truck/{TruckId}/earnings");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetEarningsRequest req, CancellationToken ct)
    {
        var earnings = await _manager.GetEarningsAsync(req.TruckId, ct);
        await Send.OkAsync(earnings, ct);
    }
}