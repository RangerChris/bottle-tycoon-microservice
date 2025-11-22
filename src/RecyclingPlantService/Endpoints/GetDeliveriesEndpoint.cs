using FastEndpoints;
using RecyclingPlantService.Data;
using RecyclingPlantService.Services;

namespace RecyclingPlantService.Endpoints;

public class GetDeliveriesEndpoint : Endpoint<GetDeliveriesRequest, IEnumerable<PlantDelivery>>
{
    private readonly IRecyclingPlantService _service;

    public GetDeliveriesEndpoint(IRecyclingPlantService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/api/v1/recycling-plant/deliveries");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetDeliveriesRequest req, CancellationToken ct)
    {
        var deliveries = await _service.GetDeliveriesAsync(req.Page, req.PageSize);
        await Send.OkAsync(deliveries);
    }
}

public class GetDeliveriesRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}