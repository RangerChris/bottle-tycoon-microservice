using FastEndpoints;
using RecyclerService.Services;

namespace RecyclerService.Endpoints;

public class InitializeEndpoint : EndpointWithoutRequest
{
    private readonly IRecyclerService _recyclerService;
    private readonly IRecyclerTelemetryStore _telemetryStore;

    public InitializeEndpoint(IRecyclerService recyclerService, IRecyclerTelemetryStore telemetryStore)
    {
        _recyclerService = recyclerService;
        _telemetryStore = telemetryStore;
    }

    public override void Configure()
    {
        Post("/initialize");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await _recyclerService.ResetAsync();
        _telemetryStore.RemoveAll();
        var recycler = await _recyclerService.CreateRecyclerAsync();
        _telemetryStore.Set(recycler.Id, 0);
    }
}