using FastEndpoints;
using RecyclingPlantService.Data;
using RecyclingPlantService.Services;

namespace RecyclingPlantService.Endpoints;

public class GetTopEarnersEndpoint(IRecyclingPlantService service) : Endpoint<GetTopEarnersRequest, IEnumerable<PlayerEarnings>>
{
    public override void Configure()
    {
        Get("/api/v1/recycling-plant/reports/top-earners");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetTopEarnersRequest req, CancellationToken ct)
    {
        var topEarners = await service.GetTopEarnersAsync(req.Count);
        await Send.OkAsync(topEarners, ct);
    }
}

public class GetTopEarnersRequest
{
    public int Count { get; set; } = 10;
}