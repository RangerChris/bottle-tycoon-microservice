using FastEndpoints;

namespace RecyclerService.Endpoints;

public class InitializeEndpoint : EndpointWithoutRequest
{
    private readonly Services.IRecyclerService _recyclerService;

    public InitializeEndpoint(Services.IRecyclerService recyclerService)
    {
        _recyclerService = recyclerService;
    }

    public override void Configure()
    {
        Post("/initialize");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await _recyclerService.ResetAsync();
        await _recyclerService.CreateRecyclerAsync();
    }
}