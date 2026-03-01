using FastEndpoints;
using TruckService.Services;

namespace TruckService.Endpoints.Admin;

public class ProcessNextDeliveryEndpoint(IRouteWorker worker) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/admin/routeworker/process-next");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await worker.RunOnceAsync(ct);
        await Send.OkAsync(null, ct);
    }
}