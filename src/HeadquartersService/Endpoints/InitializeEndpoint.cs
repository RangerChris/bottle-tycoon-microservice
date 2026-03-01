using FastEndpoints;
using HeadquartersService.Services;

namespace HeadquartersService.Endpoints;

public class InitializeEndpoint(IHeadquartersService headquartersService) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/initialize");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await headquartersService.ResetAsync();
        await headquartersService.InitializeFleetAsync();
    }
}