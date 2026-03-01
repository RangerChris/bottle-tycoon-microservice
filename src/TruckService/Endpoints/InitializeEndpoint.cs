using FastEndpoints;
using TruckService.Services;

namespace TruckService.Endpoints;

public class InitializeEndpoint : EndpointWithoutRequest
{
    private readonly ITruckService _truckService;
    private readonly ITruckTelemetryStore _telemetryStore;

    public InitializeEndpoint(ITruckService truckService, ITruckTelemetryStore telemetryStore)
    {
        _truckService = truckService;
        _telemetryStore = telemetryStore;
    }

    public override void Configure()
    {
        Post("/initialize");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await _truckService.ResetAsync();
        _telemetryStore.RemoveAll();
        await _truckService.CreateTruckAsync();
    }
}