using FastEndpoints;
using RecyclingPlantService.Data;
using RecyclingPlantService.Services;

namespace RecyclingPlantService.Endpoints;

public class GetTopEarnersEndpoint : Endpoint<GetTopEarnersRequest, IEnumerable<PlayerEarnings>>
{
    private readonly IRecyclingPlantService _service;

    public GetTopEarnersEndpoint(IRecyclingPlantService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/api/v1/recycling-plant/reports/top-earners");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetTopEarnersRequest req, CancellationToken ct)
    {
        var topEarners = await _service.GetTopEarnersAsync(req.Count);
        await Send.OkAsync(topEarners);
    }
}

public class GetTopEarnersRequest
{
    public int Count { get; set; } = 10;
}