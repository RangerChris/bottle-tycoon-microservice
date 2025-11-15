using GameService.Data;
using GameService.Models;
using Microsoft.EntityFrameworkCore;

namespace GameService.Services;

public class PlayerService : IPlayerService
{
    private readonly GameDbContext _context;

    public PlayerService(GameDbContext context)
    {
        _context = context;
    }

    public async Task<Player?> GetPlayerAsync(Guid playerId)
    {
        return await _context.Players
            .Include(p => p.Purchases)
            .Include(p => p.Upgrades)
            .FirstOrDefaultAsync(p => p.Id == playerId);
    }

    public async Task<Player> CreatePlayerAsync()
    {
        var player = new Player();
        _context.Players.Add(player);
        await _context.SaveChangesAsync();
        return player;
    }

    public async Task<bool> DebitCreditsAsync(Guid playerId, decimal amount, string reason)
    {
        var player = await _context.Players.FindAsync(playerId);
        if (player == null || player.Credits < amount)
            return false;

        player.Credits -= amount;
        player.UpdatedAt = DateTime.UtcNow;

        // Log the transaction (could be an event or audit table)
        var purchase = new Purchase
        {
            PlayerId = playerId,
            ItemType = reason,
            Amount = amount
        };
        _context.Purchases.Add(purchase);

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CreditCreditsAsync(Guid playerId, decimal amount, string reason)
    {
        var player = await _context.Players.FindAsync(playerId);
        if (player == null)
            return false;

        player.Credits += amount;
        player.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> PurchaseItemAsync(Guid playerId, string itemType, decimal cost)
    {
        return await DebitCreditsAsync(playerId, cost, $"Purchase-{itemType}");
    }

    public async Task<bool> UpgradeItemAsync(Guid playerId, string itemType, int itemId, int newLevel, decimal cost)
    {
        var success = await DebitCreditsAsync(playerId, cost, $"Upgrade-{itemType}-{itemId}");
        if (success)
        {
            var upgrade = new Upgrade
            {
                PlayerId = playerId,
                ItemType = itemType,
                ItemId = itemId,
                NewLevel = newLevel,
                Cost = cost
            };
            _context.Upgrades.Add(upgrade);
            await _context.SaveChangesAsync();
        }
        return success;
    }
}