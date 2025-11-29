using FastEndpoints;

namespace HeadquartersService.Endpoints;

public class GetRootEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/ping");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.ContentType = "text/plain";
        await HttpContext.Response.WriteAsync("pong", ct);
    }
}