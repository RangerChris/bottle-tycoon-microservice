using TruckService.Models;

namespace TruckService.Services;

public interface ITruckService
{
    Task ResetAsync();
    Task<TruckDto> CreateTruckAsync(TruckDto? truck = null);
}