using FastEndpoints;
using RecyclerService.Models;
using RecyclerService.Services;

namespace RecyclerService.Endpoints;

public class VisitorArrivedEndpoint : Endpoint<VisitorArrivedEndpoint.Request, VisitorArrivedEndpoint.VisitorResponse>
{
    private readonly IRecyclerService _service;

    public VisitorArrivedEndpoint(IRecyclerService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Verbs("POST");
        Routes("/recyclers/{id:guid}/visitors");
        AllowAnonymous();
        Options(x => x.WithTags("Recycler"));
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var recyclerId = Route<Guid>("id");
        var visitor = new Visitor { Bottles = req.Bottles, VisitorType = req.VisitorType };
        try
        {
            var recycler = await _service.VisitorArrivedAsync(recyclerId, visitor, ct);
            await Send.ResultAsync(TypedResults.Ok(new VisitorResponse { RecyclerId = recycler.Id, CurrentLoad = recycler.CurrentLoad, Capacity = recycler.Capacity }));
        }
        catch (KeyNotFoundException)
        {
            ThrowError("Recycler not found", 404);
        }
    }

    public record Request
    {
        public int Bottles { get; set; }
        public string? VisitorType { get; set; }
    }

    public record VisitorResponse
    {
        public Guid RecyclerId { get; set; }
        public int CurrentLoad { get; set; }
        public int Capacity { get; set; }
    }
}