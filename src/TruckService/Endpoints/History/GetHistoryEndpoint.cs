using FastEndpoints;
using TruckService.Services;

namespace TruckService.Endpoints.History;

public class GetHistoryEndpoint : Endpoint<GetHistoryRequest, IEnumerable<object>>
{
    private readonly ITruckManager _manager;

    public GetHistoryEndpoint(ITruckManager manager)
    {
        _manager = manager;
    }

    public override void Configure()
    {
        Get("/api/v1/truck/{TruckId}/history");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetHistoryRequest req, CancellationToken ct)
    {
        var history = await _manager.GetHistoryAsync(req.TruckId, ct);
        await Send.OkAsync(history, ct);
    }
}