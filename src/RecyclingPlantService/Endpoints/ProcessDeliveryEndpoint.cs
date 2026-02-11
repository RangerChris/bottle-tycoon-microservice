using FastEndpoints;
using RecyclingPlantService.Services;

namespace RecyclingPlantService.Endpoints;

public sealed class ProcessDeliveryEndpoint : Endpoint<ProcessDeliveryEndpoint.Request, ProcessDeliveryEndpoint.Response>
{
    private readonly IRecyclingPlantService _plantService;
    private readonly ILogger<ProcessDeliveryEndpoint> _logger;

    public ProcessDeliveryEndpoint(IRecyclingPlantService plantService, ILogger<ProcessDeliveryEndpoint> logger)
    {
        _plantService = plantService;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/deliveries");
        AllowAnonymous();
        Options(x => x.WithTags("RecyclingPlant"));
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        if (req.TruckId == Guid.Empty)
        {
            await Send.ErrorsAsync(400, ct);
            AddError("Invalid truck ID");
            return;
        }

        if (req.PlayerId == Guid.Empty)
        {
            await Send.ErrorsAsync(400, ct);
            AddError("Invalid player ID");
            return;
        }

        if (req.LoadByType.Count == 0)
        {
            await Send.ErrorsAsync(400, ct);
            AddError("No bottles in load");
            return;
        }

        var deliveryId = await _plantService.ProcessDeliveryAsync(
            req.TruckId,
            req.PlayerId,
            req.LoadByType,
            req.OperatingCost,
            DateTimeOffset.UtcNow);

        var (gross, net) = _plantService.CalculateEarnings(req.LoadByType, req.OperatingCost);

        _logger.LogInformation(
            "Delivery processed: ID={DeliveryId}, Truck={TruckId}, Player={PlayerId}, Gross={Gross}, Net={Net}",
            deliveryId, req.TruckId, req.PlayerId, gross, net);

        await Send.OkAsync(new Response
        {
            Success = true,
            DeliveryId = deliveryId,
            GrossEarnings = gross,
            NetEarnings = net,
            Message = "Delivery processed successfully"
        }, ct);
    }

    public sealed record Request
    {
        public Guid TruckId { get; init; }
        public Guid PlayerId { get; init; }
        public Dictionary<string, int> LoadByType { get; init; } = new();
        public decimal OperatingCost { get; init; }
    }

    public new sealed record Response
    {
        public bool Success { get; init; }
        public Guid? DeliveryId { get; init; }
        public decimal GrossEarnings { get; init; }
        public decimal NetEarnings { get; init; }
        public string Message { get; init; } = string.Empty;
    }
}