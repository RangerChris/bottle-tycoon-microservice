using RecyclingPlantService.Data;

namespace RecyclingPlantService.Services;

public interface IRecyclingPlantService
{
    (decimal GrossEarnings, decimal NetEarnings) CalculateEarnings(IDictionary<string, int> loadByType, decimal operatingCost);
    Task<Guid> ProcessDeliveryAsync(Guid truckId, Guid playerId, IDictionary<string, int> loadByType, decimal operatingCost, DateTimeOffset deliveredAt);
    Task<PlayerEarnings> GetPlayerEarningsAsync(Guid playerId);
    Task<EarningsBreakdown> GetPlayerEarningsBreakdownAsync(Guid playerId);
    Task<IEnumerable<PlantDelivery>> GetDeliveriesAsync(int page = 1, int pageSize = 50);
    Task<IEnumerable<PlayerEarnings>> GetTopEarnersAsync(int count = 10);
    Task<IEnumerable<PlantDelivery>> GetPlayerDeliveriesAsync(Guid playerId, int page = 1, int pageSize = 50);
    Task ResetAsync();
    Task CreateRecyclingPlantAsync();
}