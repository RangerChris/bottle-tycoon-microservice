using MassTransit;
using RecyclerService.Events;
using RecyclerService.Services;

namespace RecyclerService.Consumers;

public class TruckArrivedConsumer : IConsumer<TruckArrived>
{
    private readonly ILogger<TruckArrivedConsumer> _logger;
    private readonly IRecyclerService _recyclerService;

    public TruckArrivedConsumer(IRecyclerService recyclerService, ILogger<TruckArrivedConsumer> logger)
    {
        _recyclerService = recyclerService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TruckArrived> context)
    {
        _logger.LogInformation("Received TruckArrived for Recycler {RecyclerId} from Truck {TruckId}", context.Message.RecyclerId, context.Message.TruckId);

        // TODO: implement loading logic. For now just log and return.
        await Task.CompletedTask;
    }
}