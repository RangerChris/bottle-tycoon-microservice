using FastEndpoints;
using TruckService.Services;

namespace TruckService.Endpoints.Admin;

public class ProcessNextDeliveryEndpoint : EndpointWithoutRequest
{
    private readonly IRouteWorker _worker;

    public ProcessNextDeliveryEndpoint(IRouteWorker worker)
    {
        _worker = worker;
    }

    public override void Configure()
    {
        Post("/admin/routeworker/process-next");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await _worker.RunOnceAsync(ct);
        await Send.OkAsync(ct);
    }
}