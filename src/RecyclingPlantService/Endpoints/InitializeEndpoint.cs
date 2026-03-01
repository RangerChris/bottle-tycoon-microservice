using FastEndpoints;
using RecyclingPlantService.Services;

namespace RecyclingPlantService.Endpoints;

public class InitializeEndpoint(IRecyclingPlantService recyclingPlantService) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/initialize");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await recyclingPlantService.ResetAsync();
        await recyclingPlantService.CreateRecyclingPlantAsync();
    }
}