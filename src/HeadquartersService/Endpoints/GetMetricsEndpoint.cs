using FastEndpoints;
using HeadquartersService.Models;
using HeadquartersService.Services;

namespace HeadquartersService.Endpoints;

public class GetMetricsEndpoint : EndpointWithoutRequest
{
    private readonly IFleetService _fleet;
    private readonly IDispatchQueue _queue;

    public GetMetricsEndpoint(IDispatchQueue queue, IFleetService fleet)
    {
        _queue = queue;
        _fleet = fleet;
    }

    public override void Configure()
    {
        Get("/api/v1/headquarters/metrics");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        var pending = _queue.PeekAll().Count;
        var trucks = _fleet.GetAll();
        var utilization = trucks.Any() ? trucks.Count(t => t.Status != TruckStatus.Idle) / (double)trucks.Count : 0.0;

        var dto = new
        {
            pendingDispatches = pending,
            truckCount = trucks.Count,
            utilization
        };

        Send.OkAsync(dto, ct);
        return Task.CompletedTask;
    }
}