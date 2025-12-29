﻿using GameService.Models;

namespace GameService.Services;

public interface IPlayerService
{
    Task<Player?> GetPlayerAsync(Guid playerId);
    Task<List<Player>> GetAllPlayersAsync();
    Task<Player> CreatePlayerAsync(Player? player = null);
    Task UpdatePlayerAsync(Player player);
    Task<bool> DebitCreditsAsync(Guid playerId, decimal amount, string reason);
    Task<bool> CreditCreditsAsync(Guid playerId, decimal amount, string reason);
    Task ResetAsync();
}