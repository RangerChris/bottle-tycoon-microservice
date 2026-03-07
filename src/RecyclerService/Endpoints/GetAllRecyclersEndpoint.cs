using FastEndpoints;
using RecyclerService.Services;

namespace RecyclerService.Endpoints;

public class GetAllRecyclersEndpoint(IRecyclerService service, IRecyclerTelemetryStore telemetryStore) : EndpointWithoutRequest<List<GetAllRecyclersEndpoint.RecyclerResponse>>
{
    public override void Configure()
    {
        Get("/recyclers");
        AllowAnonymous();
        Options(x => x.WithTags("Recycler"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var recyclers = await service.GetAllAsync(ct);
        var activeRecyclers = recyclers.Where(r => !r.IsBlockedForSale).ToList();

        foreach (var recycler in activeRecyclers)
        {
            telemetryStore.MarkActive(recycler.Id, recycler.Name);
        }

        var response = activeRecyclers
            .Select(r => new RecyclerResponse { Id = r.Id, Name = r.Name, CurrentLoad = r.CurrentLoad, Capacity = r.Capacity, CapacityLevel = r.CapacityLevel })
            .ToList();
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