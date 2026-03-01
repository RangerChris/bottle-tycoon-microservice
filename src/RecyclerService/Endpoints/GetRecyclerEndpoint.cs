using FastEndpoints;
using RecyclerService.Services;

namespace RecyclerService.Endpoints;

public class GetRecyclerEndpoint(IRecyclerService service) : EndpointWithoutRequest<GetRecyclerEndpoint.RecyclerResponse>
{
    public override void Configure()
    {
        Verbs("GET");
        Routes("/recyclers/{id:guid}");
        AllowAnonymous();
        Options(x => x.WithTags("Recycler"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");

        var recycler = await service.GetByIdAsync(id, ct);
        if (recycler == null)
        {
            ThrowError("Recycler not found", 404);
            return;
        }

        await Send.ResultAsync(TypedResults.Ok(new RecyclerResponse { Id = recycler.Id, Name = recycler.Name, CurrentLoad = recycler.CurrentLoad, Capacity = recycler.Capacity }));
    }

    public record RecyclerResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public int CurrentLoad { get; set; }
        public int Capacity { get; set; }
    }
}