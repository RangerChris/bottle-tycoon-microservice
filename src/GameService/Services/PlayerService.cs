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
        return await _context.Players.FirstOrDefaultAsync(p => p.Id == playerId);
    }

    public async Task<List<Player>> GetAllPlayersAsync()
    {
        return await _context.Players.ToListAsync();
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
        if (p.Credits == 0)
        {
            p.Credits = 1000;
        }

        _context.Players.Add(p);
        await _context.SaveChangesAsync();
        return p;
    }

    public async Task<bool> DebitCreditsAsync(Guid playerId, decimal amount, string reason)
    {
        var player = await _context.Players.FindAsync(playerId);
        if (player == null || player.Credits < amount)
        {
            return false;
        }

        player.Credits -= amount;
        player.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CreditCreditsAsync(Guid playerId, decimal amount, string reason)
    {
        var player = await _context.Players.FindAsync(playerId);
        if (player == null)
        {
            return false;
        }

        player.Credits += amount;
        player.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task ResetAsync()
    {
        await _context.Players.ExecuteDeleteAsync();
    }
}