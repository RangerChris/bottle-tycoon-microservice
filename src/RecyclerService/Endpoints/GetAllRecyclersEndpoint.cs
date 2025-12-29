using FastEndpoints;
using RecyclerService.Services;

namespace RecyclerService.Endpoints;

public class GetAllRecyclersEndpoint : EndpointWithoutRequest<List<GetAllRecyclersEndpoint.RecyclerResponse>>
{
    private readonly IRecyclerService _service;

    public GetAllRecyclersEndpoint(IRecyclerService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/recyclers");
        AllowAnonymous();
        Options(x => x.WithTags("Recycler"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var recyclers = await _service.GetAllAsync(ct);
        var response = recyclers.Select(r => new RecyclerResponse { Id = r.Id, Name = r.Name, CurrentLoad = r.CurrentLoad, Capacity = r.Capacity, CapacityLevel = r.CapacityLevel }).ToList();
        await Send.ResultAsync(TypedResults.Ok(response));
    }

    public record RecyclerResponse
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = null!;
        public int CurrentLoad { get; init; }
        public int Capacity { get; init; }
        public int CapacityLevel { get; init; }
    }
}