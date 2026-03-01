using FastEndpoints;
using RecyclerService.Services;

namespace RecyclerService.Endpoints;

public class InitializeEndpoint(IRecyclerService recyclerService, IRecyclerTelemetryStore telemetryStore) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/initialize");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await recyclerService.ResetAsync();
        telemetryStore.RemoveAll();
        var recycler = await recyclerService.CreateRecyclerAsync();
        telemetryStore.Set(recycler.Id, recycler.Name, 0, 0, 0);
    }
}