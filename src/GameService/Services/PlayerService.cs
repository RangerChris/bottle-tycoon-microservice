using GameService.Data;
using GameService.Models;
using Microsoft.EntityFrameworkCore;

namespace GameService.Services;

public class PlayerService(GameDbContext context) : IPlayerService
{
    public async Task<Player?> GetPlayerAsync(Guid playerId)
    {
        return await context.Players.FirstOrDefaultAsync(p => p.Id == playerId);
    }

    public async Task<List<Player>> GetAllPlayersAsync()
    {
        return await context.Players.ToListAsync();
    }

    public async Task<Player> CreatePlayerAsync(Player? player = null)
    {
        var p = player ?? new Player();
        if (p.Id == Guid.Empty)
        {
            p.Id = Guid.NewGuid();
        }

        p.CreatedAt = DateTime.UtcNow;
        p.UpdatedAt = DateTime.UtcNow;
        // Always set starting credits for new players
        p.Credits = 1300;

        context.Players.Add(p);
        await context.SaveChangesAsync();
        return p;
    }

    public async Task<bool> DebitCreditsAsync(Guid playerId, decimal amount, string reason)
    {
        var player = await context.Players.FindAsync(playerId);
        if (player == null || player.Credits < amount)
        {
            return false;
        }

        player.Credits -= amount;
        player.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CreditCreditsAsync(Guid playerId, decimal amount, string reason)
    {
        var player = await context.Players.FindAsync(playerId);
        if (player == null)
        {
            return false;
        }

        player.Credits += amount;
        player.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return true;
    }

    public async Task ResetAsync()
    {
        await context.Players.ExecuteDeleteAsync();
    }

    public async Task UpdatePlayerAsync(Player player)
    {
        context.Players.Update(player);
        await context.SaveChangesAsync();
    }
}