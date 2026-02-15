using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using RecyclingPlantService.Data;

namespace RecyclingPlantService.Services;

public class RecyclingPlantService : IRecyclingPlantService
{
    private static readonly Meter Meter = new("RecyclingPlantService");
    private static readonly Counter<long> DeliveriesProcessed = Meter.CreateCounter<long>("deliveries_processed", unit: "deliveries", description: "Number of deliveries processed");
    private static readonly Counter<long> BottlesReceived = Meter.CreateCounter<long>("bottles_received", unit: "bottles", description: "Number of bottles received by type");
    private static readonly Histogram<double> EarningsDistributed = Meter.CreateHistogram<double>("earnings_distributed", "credits", "Earnings distributed");
    private static readonly Histogram<double> OperatingCosts = Meter.CreateHistogram<double>("operating_costs", "credits", "Operating costs incurred");

    private static readonly Dictionary<string, decimal> CreditRates = new()
    {
        { "glass", 4.0m },
        { "metal", 2.5m },
        { "plastic", 1.75m }
    };

    private readonly RecyclingPlantDbContext _dbContext;
    private readonly ILogger<RecyclingPlantService> _logger;

    public RecyclingPlantService(RecyclingPlantDbContext dbContext, ILogger<RecyclingPlantService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public (decimal GrossEarnings, decimal NetEarnings) CalculateEarnings(IDictionary<string, int> loadByType, decimal operatingCost)
    {
        decimal gross = 0;
        foreach (var (type, count) in loadByType)
        {
            if (CreditRates.TryGetValue(type, out var rate))
            {
                gross += count * rate;
            }
        }

        var net = gross - operatingCost;
        return (gross, net);
    }

    public async Task<Guid> ProcessDeliveryAsync(Guid truckId, Guid playerId, IDictionary<string, int> loadByType, decimal operatingCost, DateTimeOffset deliveredAt)
    {
        var (gross, net) = CalculateEarnings(loadByType, operatingCost);

        var delivery = new PlantDelivery
        {
            Id = Guid.NewGuid(),
            TruckId = truckId,
            PlayerId = playerId,
            GlassCount = loadByType.TryGetValue("glass", out var glass) ? glass : 0,
            MetalCount = loadByType.TryGetValue("metal", out var metal) ? metal : 0,
            PlasticCount = loadByType.TryGetValue("plastic", out var plastic) ? plastic : 0,
            GrossEarnings = gross,
            OperatingCost = operatingCost,
            NetEarnings = net,
            DeliveredAt = deliveredAt
        };

        _dbContext.PlantDeliveries.Add(delivery);

        var playerEarnings = await _dbContext.PlayerEarnings.FindAsync(playerId);
        if (playerEarnings == null)
        {
            playerEarnings = new PlayerEarnings { PlayerId = playerId };
            _dbContext.PlayerEarnings.Add(playerEarnings);
        }

        playerEarnings.TotalEarnings += net;
        playerEarnings.DeliveryCount++;
        playerEarnings.AverageEarnings = playerEarnings.TotalEarnings / playerEarnings.DeliveryCount;
        playerEarnings.LastUpdated = DateTimeOffset.UtcNow;

        DeliveriesProcessed.Add(1, new KeyValuePair<string, object?>("truck_id", truckId.ToString()), new KeyValuePair<string, object?>("player_id", playerId.ToString()));
        BottlesReceived.Add(delivery.GlassCount, new KeyValuePair<string, object?>("type", "glass"));
        BottlesReceived.Add(delivery.MetalCount, new KeyValuePair<string, object?>("type", "metal"));
        BottlesReceived.Add(delivery.PlasticCount, new KeyValuePair<string, object?>("type", "plastic"));
        EarningsDistributed.Record((double)gross, new KeyValuePair<string, object?>("truck_id", truckId.ToString()), new KeyValuePair<string, object?>("player_id", playerId.ToString()));
        OperatingCosts.Record((double)operatingCost, new KeyValuePair<string, object?>("truck_id", truckId.ToString()), new KeyValuePair<string, object?>("player_id", playerId.ToString()));

        await _dbContext.SaveChangesAsync();

        return delivery.Id;
    }

    public async Task<PlayerEarnings> GetPlayerEarningsAsync(Guid playerId)
    {
        return await _dbContext.PlayerEarnings.FindAsync(playerId) ?? new PlayerEarnings { PlayerId = playerId };
    }

    public async Task<IEnumerable<PlantDelivery>> GetDeliveriesAsync(int page = 1, int pageSize = 50)
    {
        return await _dbContext.PlantDeliveries
            .OrderByDescending(d => d.DeliveredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<IEnumerable<PlayerEarnings>> GetTopEarnersAsync(int count = 10)
    {
        return await _dbContext.PlayerEarnings
            .OrderByDescending(pe => pe.TotalEarnings)
            .Take(count)
            .ToListAsync();
    }

    public async Task<IEnumerable<PlantDelivery>> GetPlayerDeliveriesAsync(Guid playerId, int page = 1, int pageSize = 50)
    {
        return await _dbContext.PlantDeliveries
            .Where(d => d.PlayerId == playerId)
            .OrderByDescending(d => d.DeliveredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<EarningsBreakdown> GetPlayerEarningsBreakdownAsync(Guid playerId)
    {
        var deliveries = await _dbContext.PlantDeliveries
            .Where(d => d.PlayerId == playerId)
            .ToListAsync();

        var breakdown = new EarningsBreakdown();
        foreach (var delivery in deliveries)
        {
            breakdown.GlassEarnings += delivery.GlassCount * 4.0m;
            breakdown.MetalEarnings += delivery.MetalCount * 2.5m;
            breakdown.PlasticEarnings += delivery.PlasticCount * 1.75m;
            breakdown.GlassCount += delivery.GlassCount;
            breakdown.MetalCount += delivery.MetalCount;
            breakdown.PlasticCount += delivery.PlasticCount;
        }

        return breakdown;
    }

    public async Task ResetAsync()
    {
        _dbContext.PlantDeliveries.RemoveRange(_dbContext.PlantDeliveries);
        _dbContext.PlayerEarnings.RemoveRange(_dbContext.PlayerEarnings);
        await _dbContext.SaveChangesAsync();
    }

    public Task CreateRecyclingPlantAsync()
    {
        // Recycling plant doesn't need specific initialization - it's ready to process deliveries
        return Task.CompletedTask;
    }
}