using FastEndpoints;
using RecyclerService.Services;

namespace RecyclerService.Endpoints;

public class MarkCustomerDoneEndpoint : EndpointWithoutRequest<MarkCustomerDoneEndpoint.MarkDoneResponse>
{
    private readonly IRecyclerService _service;

    public MarkCustomerDoneEndpoint(IRecyclerService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Verbs("POST");
        Routes("/recyclers/{recyclerId:guid}/customers/{customerId:guid}/done");
        AllowAnonymous();
        Options(x => x.WithTags("Recycler"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var customerId = Route<Guid>("customerId");

        await _service.MarkCustomerDoneAsync(customerId, ct);

        await Send.ResultAsync(TypedResults.Ok(new MarkDoneResponse
        {
            Success = true,
            CustomerId = customerId,
            Message = "Customer marked as done"
        }));
    }

    public record MarkDoneResponse
    {
        public bool Success { get; set; }
        public Guid CustomerId { get; set; }
        public string? Message { get; set; }
    }
}