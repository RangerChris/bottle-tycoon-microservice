using MassTransit;
using RecyclingPlantService.Services;
using Shared.Events;

namespace RecyclingPlantService.Consumers;

public class TruckLoadedConsumer : IConsumer<TruckLoaded>
{
    private readonly ILogger<TruckLoadedConsumer> _logger;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IRecyclingPlantService _recyclingPlantService;

    public TruckLoadedConsumer(ILogger<TruckLoadedConsumer> logger, IRecyclingPlantService recyclingPlantService, IPublishEndpoint publishEndpoint)
    {
        _logger = logger;
        _recyclingPlantService = recyclingPlantService;
        _publishEndpoint = publishEndpoint;
    }

    public async Task Consume(ConsumeContext<TruckLoaded> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing delivery from Truck {TruckId} for Player {PlayerId} with load {Load}", message.TruckId, message.PlayerId, string.Join(", ", message.LoadByType.Select(kv => $"{kv.Key}:{kv.Value}")));

        try
        {
            // Validate bottle counts
            if (message.LoadByType.Any(kv => kv.Value < 0))
            {
                _logger.LogWarning("Invalid bottle counts in delivery from Truck {TruckId}", message.TruckId);
                return;
            }

            // Process the delivery
            var deliveryId = await _recyclingPlantService.ProcessDeliveryAsync(message.TruckId, message.PlayerId, message.LoadByType, message.OperatingCost, message.LoadedAt);

            var (gross, net) = _recyclingPlantService.CalculateEarnings(message.LoadByType, message.OperatingCost);

            // Publish EarningsCalculated
            var earningsCalculated = new EarningsCalculated(
                deliveryId,
                message.PlayerId,
                gross,
                message.OperatingCost,
                net,
                DateTimeOffset.UtcNow
            );
            await _publishEndpoint.Publish(earningsCalculated);

            // Publish EarningsPublished
            var earningsPublished = new EarningsPublished(
                deliveryId,
                message.PlayerId,
                net,
                DateTimeOffset.UtcNow
            );
            await _publishEndpoint.Publish(earningsPublished);

            _logger.LogInformation("Processed delivery for Truck {TruckId}, net earnings {NetEarnings}", message.TruckId, net);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process delivery from Truck {TruckId}", message.TruckId);
            // In a real system, might want to send to dead letter queue or retry
        }
    }
}