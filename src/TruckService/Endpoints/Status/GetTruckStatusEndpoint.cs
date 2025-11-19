using FastEndpoints;
using TruckService.Models;
using TruckService.Services;

namespace TruckService.Endpoints.Status;

public class GetTruckStatusEndpoint : Endpoint<GetTruckStatusRequest, TruckStatusDto>
{
    private readonly ITruckManager _manager;

    public GetTruckStatusEndpoint(ITruckManager manager)
    {
        _manager = manager;
    }

    public override void Configure()
    {
        Get("/api/v1/trucks/{Id}/status");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetTruckStatusRequest req, CancellationToken ct)
    {
        try
        {
            var status = await _manager.GetStatusAsync(req.Id, ct);
            await Send.OkAsync(status, ct);
        }
        catch (KeyNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}