namespace TruckService.Services;

public interface IRouteWorker
{
    Task RunOnceAsync(CancellationToken ct = default);
}