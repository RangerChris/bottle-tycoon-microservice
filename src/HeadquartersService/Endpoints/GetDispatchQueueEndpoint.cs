using FastEndpoints;
using HeadquartersService.Services;

namespace HeadquartersService.Endpoints;

public class GetDispatchQueueEndpoint(IDispatchQueue queue) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/api/v1/headquarters/dispatch-queue");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        var q = queue.PeekAll();
        Send.OkAsync(q, ct);
        return Task.CompletedTask;
    }
}