﻿namespace TruckService.Services;

public class RandomLoadProvider : ILoadProvider
{
    private readonly Random _rng = new();

    public Task<(int glass, int metal, int plastic)> GetLoadForRecyclerAsync(Guid recyclerId, int maxCapacity, CancellationToken ct = default)
    {
        return Task.FromResult((_rng.Next(0, 30), _rng.Next(0, 20), _rng.Next(0, 25)));
    }
}