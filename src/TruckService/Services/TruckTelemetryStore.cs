using System.Collections.Concurrent;

namespace TruckService.Services;

public interface ITruckTelemetryStore
{
    void Set(Guid truckId, int currentLoad, int capacity, string status);
    void RemoveAll();
    IReadOnlyCollection<TruckTelemetrySnapshot> GetAll();
}

public sealed record TruckTelemetrySnapshot(Guid TruckId, int CurrentLoad, int Capacity, string Status);

public sealed class TruckTelemetryStore : ITruckTelemetryStore
{
    private readonly ConcurrentDictionary<Guid, TruckTelemetrySnapshot> _trucksById = new();

    public void Set(Guid truckId, int currentLoad, int capacity, string status)
    {
        var sanitizedLoad = currentLoad < 0 ? 0 : currentLoad;
        var sanitizedCapacity = capacity < 0 ? 0 : capacity;
        _trucksById[truckId] = new TruckTelemetrySnapshot(truckId, sanitizedLoad, sanitizedCapacity, status);
    }

    public void RemoveAll()
    {
        _trucksById.Clear();
    }

    public IReadOnlyCollection<TruckTelemetrySnapshot> GetAll()
    {
        return _trucksById.Values.ToArray();
    }
}