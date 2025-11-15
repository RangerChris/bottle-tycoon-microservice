using GameService.Events;
using GameService.Services;
using MassTransit;

namespace GameService.Consumers;

public class CreditsCreditedConsumer : IConsumer<CreditsCredited>
{
    private readonly IPlayerService _playerService;
    private readonly ILogger<CreditsCreditedConsumer> _logger;

    public CreditsCreditedConsumer(IPlayerService playerService, ILogger<CreditsCreditedConsumer> logger)
    {
        _playerService = playerService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CreditsCredited> context)
    {
        var message = context.Message;
        _logger.LogInformation("Crediting {Amount} credits to player {PlayerId} for {Reason}",
            message.Amount, message.PlayerId, message.Reason);

        var success = await _playerService.CreditCreditsAsync(message.PlayerId, message.Amount, message.Reason);
        if (!success)
        {
            _logger.LogWarning("Failed to credit credits to player {PlayerId}", message.PlayerId);
            throw new Exception($"Failed to credit credits to player {message.PlayerId}");
        }
    }
}