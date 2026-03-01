using FastEndpoints;
using HeadquartersService.Services;

namespace HeadquartersService.Endpoints;

public class GetDispatchDetailEndpoint(IDispatchQueue queue) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/api/v1/headquarters/dispatch/{id}");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        var idStr = Route<string>("id");
        if (!Guid.TryParse(idStr, out var id))
        {
            Send.NotFoundAsync(ct);
            return Task.CompletedTask;
        }

        var req = queue.Get(id);
        if (req == null)
        {
            Send.NotFoundAsync(ct);
            return Task.CompletedTask;
        }

        Send.OkAsync(req, ct);
        return Task.CompletedTask;
    }
}