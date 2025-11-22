using FastEndpoints;

namespace RecyclingPlantService.Endpoints;

public class GetStatusEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/api/v1/recycling-plant/status");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(new
        {
            Status = "Operational",
            Timestamp = DateTimeOffset.UtcNow
        });
    }
}