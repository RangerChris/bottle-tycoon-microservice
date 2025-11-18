using TruckService.Models;

namespace TruckService.Services;

public interface ITruckManager
{
    Task<TruckStatusDto> GetStatusAsync(Guid truckId, CancellationToken ct = default);
    Task<bool> DispatchAsync(Guid truckId, Guid recyclerId, double distanceKm, CancellationToken ct = default);
    Task<IEnumerable<TruckStatusDto>> GetFleetSummaryAsync(CancellationToken ct = default);
    Task<IEnumerable<object>> GetHistoryAsync(Guid truckId, CancellationToken ct = default);
    Task<decimal> GetEarningsAsync(Guid truckId, CancellationToken ct = default);
}