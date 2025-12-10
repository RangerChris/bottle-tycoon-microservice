namespace RecyclerService.Consumers;

public class TruckLoadedConsumer
 {
     private readonly ILogger<TruckLoadedConsumer> _logger;

     public TruckLoadedConsumer(ILogger<TruckLoadedConsumer> logger)
     {
         _logger = logger;
     }

     public async Task HandleAsync()
     {
         await Task.CompletedTask;
     }
 }