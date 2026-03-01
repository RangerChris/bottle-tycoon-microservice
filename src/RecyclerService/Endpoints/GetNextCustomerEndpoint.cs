using FastEndpoints;
using RecyclerService.Services;

namespace RecyclerService.Endpoints;

public class GetNextCustomerEndpoint(IRecyclerService service) : EndpointWithoutRequest<GetNextCustomerEndpoint.NextCustomerResponse>
{
    public override void Configure()
    {
        Verbs("GET");
        Routes("/recyclers/{id:guid}/next-customer");
        AllowAnonymous();
        Options(x => x.WithTags("Recycler"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var recyclerId = Route<Guid>("id");

        var customer = await service.GetNextCustomerAsync(recyclerId, ct);

        if (customer == null)
        {
            await Send.ResultAsync(TypedResults.Ok(new NextCustomerResponse()));
        }
        else
        {
            var bottleCounts = customer.GetBottleCounts();
            await Send.ResultAsync(TypedResults.Ok(new NextCustomerResponse
            {
                CustomerId = customer.Id,
                RecyclerId = customer.RecyclerId,
                Glass = bottleCounts.GetValueOrDefault("glass", 0),
                Metal = bottleCounts.GetValueOrDefault("metal", 0),
                Plastic = bottleCounts.GetValueOrDefault("plastic", 0),
                Total = customer.Bottles,
                Status = customer.Status.ToString()
            }));
        }
    }

    public record NextCustomerResponse
    {
        public Guid CustomerId { get; set; }
        public Guid RecyclerId { get; set; }
        public int Glass { get; set; }
        public int Metal { get; set; }
        public int Plastic { get; set; }
        public int Total { get; set; }
        public string? Status { get; set; }
    }
}