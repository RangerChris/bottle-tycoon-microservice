using GameService.Events;
using GameService.Services;

namespace GameService.Consumers;

public class CreditsCreditedConsumer
{
    private readonly ILogger<CreditsCreditedConsumer> _logger;
    private readonly IPlayerService _playerService;

    public CreditsCreditedConsumer(IPlayerService playerService, ILogger<CreditsCreditedConsumer> logger)
    {
        _playerService = playerService;
        _logger = logger;
    }

    public async Task HandleAsync(CreditsCredited message)
    {
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