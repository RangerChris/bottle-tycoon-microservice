using TruckService.Data;
using TruckService.Models;

namespace TruckService.Services;

public interface ITruckRepository
{
    Task<TruckDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TruckEntity?> GetEntityByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<TruckDto>> GetAllAsync(CancellationToken ct = default);
    Task<bool> UpdateAsync(TruckDto truck, CancellationToken ct = default);
    Task<bool> UpdateAsync(TruckEntity truck, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}