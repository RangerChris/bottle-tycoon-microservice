using RecyclingPlantService.Services;

namespace RecyclingPlantService.Consumers;

public class TruckLoadedConsumer
{
    private readonly ILogger<TruckLoadedConsumer> _logger;
    private readonly IRecyclingPlantService _recyclingPlantService;

    public TruckLoadedConsumer(ILogger<TruckLoadedConsumer> logger, IRecyclingPlantService recyclingPlantService)
    {
        _logger = logger;
        _recyclingPlantService = recyclingPlantService;
    }

    public async Task HandleAsync()
    {
        _logger.LogInformation("Processing delivery from TruckLoadedConsumer");
    }
}