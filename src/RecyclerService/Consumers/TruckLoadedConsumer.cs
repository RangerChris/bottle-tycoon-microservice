using MassTransit;
using Shared.Events;

namespace RecyclerService.Consumers;

public class TruckLoadedConsumer : IConsumer<TruckLoaded>
{
    private readonly ILogger<TruckLoadedConsumer> _logger;

    public TruckLoadedConsumer(ILogger<TruckLoadedConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TruckLoaded> context)
    {
        _logger.LogInformation("Received TruckLoaded for Recycler {RecyclerId} from Truck {TruckId} - Loaded {Load}", context.Message.RecyclerId, context.Message.TruckId, string.Join(", ", context.Message.LoadByType.Select(kv => $"{kv.Key}:{kv.Value}")));
        await Task.CompletedTask;
    }
}