using FastEndpoints;
using TruckService.Services;

namespace TruckService.Endpoints;

public class InitializeEndpoint : EndpointWithoutRequest
{
    private readonly ITruckService _truckService;

    public InitializeEndpoint(ITruckService truckService)
    {
        _truckService = truckService;
    }

    public override void Configure()
    {
        Post("/initialize");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await _truckService.ResetAsync();
        await _truckService.CreateTruckAsync();
    }
}