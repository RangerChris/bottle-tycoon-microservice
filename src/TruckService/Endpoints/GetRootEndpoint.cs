using FastEndpoints;

namespace TruckService.Endpoints;

public class GetRootEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/ping");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        // return a simple 200 OK - body isn't relied upon by existing smoke test which hits MapGet "." root
        Send.OkAsync("pong", ct);
        return Task.CompletedTask;
    }
}