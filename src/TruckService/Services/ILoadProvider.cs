namespace TruckService.Services;

public interface ILoadProvider
{
    Task<(int glass, int metal, int plastic)> GetLoadForRecyclerAsync(Guid recyclerId, int maxCapacity, CancellationToken ct = default);
}