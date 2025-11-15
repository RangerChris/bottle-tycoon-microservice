using GameService.Models;

namespace GameService.Services;

public interface IPlayerService
{
    Task<Player?> GetPlayerAsync(Guid playerId);
    Task<Player> CreatePlayerAsync();
    Task<bool> DebitCreditsAsync(Guid playerId, decimal amount, string reason);
    Task<bool> CreditCreditsAsync(Guid playerId, decimal amount, string reason);
    Task<bool> PurchaseItemAsync(Guid playerId, string itemType, decimal cost);
    Task<bool> UpgradeItemAsync(Guid playerId, string itemType, int itemId, int newLevel, decimal cost);
}