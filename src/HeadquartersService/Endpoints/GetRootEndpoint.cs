using FastEndpoints;

namespace HeadquartersService.Endpoints;

public class GetRootEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/ping");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        Send.OkAsync("pong", ct);
        return Task.CompletedTask;
    }
}