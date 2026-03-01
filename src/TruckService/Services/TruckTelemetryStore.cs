using System.Collections.Concurrent;

namespace TruckService.Services;

public interface ITruckTelemetryStore
{
    void Set(Guid truckId, string truckName, int currentLoad, int capacity, string status);
    void Remove(Guid truckId);
    void RemoveAll();
    IReadOnlyCollection<TruckTelemetrySnapshot> GetAll();
}

public sealed record TruckTelemetrySnapshot(Guid TruckId, string TruckName, int CurrentLoad, int Capacity, string Status);

public sealed class TruckTelemetryStore : ITruckTelemetryStore
{
    private readonly ConcurrentDictionary<Guid, TruckTelemetrySnapshot> _trucksById = new();

    public void Set(Guid truckId, string truckName, int currentLoad, int capacity, string status)
    {
        var sanitizedLoad = currentLoad < 0 ? 0 : currentLoad;
        var sanitizedCapacity = capacity < 0 ? 0 : capacity;
        _trucksById[truckId] = new TruckTelemetrySnapshot(truckId, truckName, sanitizedLoad, sanitizedCapacity, status);
    }

    public void Remove(Guid truckId)
    {
        _trucksById.TryRemove(truckId, out _);
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