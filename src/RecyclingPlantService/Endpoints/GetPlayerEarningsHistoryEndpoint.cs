using FastEndpoints;
using RecyclingPlantService.Data;
using RecyclingPlantService.Services;

namespace RecyclingPlantService.Endpoints;

public class GetPlayerEarningsHistoryEndpoint : Endpoint<GetPlayerEarningsHistoryRequest, IEnumerable<PlantDelivery>>
{
    private readonly IRecyclingPlantService _service;

    public GetPlayerEarningsHistoryEndpoint(IRecyclingPlantService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/api/v1/recycling-plant/players/{PlayerId}/earnings/history");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetPlayerEarningsHistoryRequest req, CancellationToken ct)
    {
        var playerId = Route<Guid>("PlayerId");
        var deliveries = await _service.GetPlayerDeliveriesAsync(playerId, req.Page, req.PageSize);
        await Send.OkAsync(deliveries);
    }
}

public class GetPlayerEarningsHistoryRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}