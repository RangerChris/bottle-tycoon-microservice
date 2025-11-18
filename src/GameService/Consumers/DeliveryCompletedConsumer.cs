// filepath: d:\projects\bottle-tycoon-microservice\src\GameService\Consumers\DeliveryCompletedConsumer.cs

using GameService.Events;
using GameService.Services;
using MassTransit;

namespace GameService.Consumers;

public class DeliveryCompletedConsumer : IConsumer<DeliveryCompleted>
{
    private readonly ILogger<DeliveryCompletedConsumer> _logger;
    private readonly IPlayerService _playerService;

    public DeliveryCompletedConsumer(IPlayerService playerService, ILogger<DeliveryCompletedConsumer> logger)
    {
        _playerService = playerService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DeliveryCompleted> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing DeliveryCompleted for Truck {TruckId} -> Plant {PlantId}, credits {Credits}", message.TruckId, message.PlantId, message.CreditsEarned);

        // If PlayerId is not provided (Guid.Empty), skip crediting
        if (message.PlayerId == Guid.Empty)
        {
            _logger.LogWarning("DeliveryCompleted message does not include PlayerId, skipping crediting");
            return;
        }

        var success = await _playerService.CreditCreditsAsync(message.PlayerId, message.CreditsEarned, "DeliveryCompleted");
        if (!success)
        {
            _logger.LogWarning("Failed to credit player {PlayerId} for delivery", message.PlayerId);
            throw new Exception($"Failed to credit player {message.PlayerId}");
        }
    }
}