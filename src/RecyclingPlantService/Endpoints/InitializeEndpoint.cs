using FastEndpoints;
using RecyclingPlantService.Services;

namespace RecyclingPlantService.Endpoints;

public class InitializeEndpoint : EndpointWithoutRequest
{
    private readonly IRecyclingPlantService _recyclingPlantService;

    public InitializeEndpoint(IRecyclingPlantService recyclingPlantService)
    {
        _recyclingPlantService = recyclingPlantService;
    }

    public override void Configure()
    {
        Post("/initialize");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await _recyclingPlantService.ResetAsync();
        await _recyclingPlantService.CreateRecyclingPlantAsync();
    }
}