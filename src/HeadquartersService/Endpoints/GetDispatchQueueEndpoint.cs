using FastEndpoints;
using HeadquartersService.Services;

namespace HeadquartersService.Endpoints;

public class GetDispatchQueueEndpoint : EndpointWithoutRequest
{
    private readonly IDispatchQueue _queue;

    public GetDispatchQueueEndpoint(IDispatchQueue queue)
    {
        _queue = queue;
    }

    public override void Configure()
    {
        Get("/api/v1/headquarters/dispatch-queue");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        var q = _queue.PeekAll();
        Send.OkAsync(q, ct);
        return Task.CompletedTask;
    }
}