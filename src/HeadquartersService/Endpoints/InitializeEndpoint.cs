using FastEndpoints;
using HeadquartersService.Services;

namespace HeadquartersService.Endpoints;

public class InitializeEndpoint : EndpointWithoutRequest
{
    private readonly IHeadquartersService _headquartersService;

    public InitializeEndpoint(IHeadquartersService headquartersService)
    {
        _headquartersService = headquartersService;
    }

    public override void Configure()
    {
        Post("/initialize");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await _headquartersService.ResetAsync();
        await _headquartersService.InitializeFleetAsync();
    }
}