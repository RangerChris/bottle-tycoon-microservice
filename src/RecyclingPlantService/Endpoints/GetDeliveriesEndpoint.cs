using FastEndpoints;
using RecyclingPlantService.Data;
using RecyclingPlantService.Services;

namespace RecyclingPlantService.Endpoints;

public class GetDeliveriesEndpoint(IRecyclingPlantService service) : Endpoint<GetDeliveriesRequest, IEnumerable<PlantDelivery>>
{
    public override void Configure()
    {
        Get("/api/v1/recycling-plant/deliveries");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetDeliveriesRequest req, CancellationToken ct)
    {
        var deliveries = await service.GetDeliveriesAsync(req.Page, req.PageSize);
        await Send.OkAsync(deliveries);
    }
}

public class GetDeliveriesRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}