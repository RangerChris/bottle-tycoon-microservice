using TruckService.Models;

namespace TruckService.Services;

public interface ITruckRepository
{
    Task<TruckDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<TruckDto>> GetAllAsync(CancellationToken ct = default);
    Task<TruckDto> CreateAsync(TruckDto truck, CancellationToken ct = default);
    Task<bool> UpdateAsync(TruckDto truck, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}