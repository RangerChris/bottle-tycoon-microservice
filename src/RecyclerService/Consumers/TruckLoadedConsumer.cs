using MassTransit;
using RecyclerService.Events;

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
        _logger.LogInformation("Received TruckLoaded for Recycler {RecyclerId} from Truck {TruckId} - Loaded {LoadedBottles}", context.Message.RecyclerId, context.Message.TruckId, context.Message.LoadedBottles);
        await Task.CompletedTask;
    }
}