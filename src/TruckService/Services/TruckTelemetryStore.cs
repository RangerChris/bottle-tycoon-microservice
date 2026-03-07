using System.Collections.Concurrent;

namespace TruckService.Services;

public interface ITruckTelemetryStore
{
    void Set(Guid truckId, string truckName, int currentLoad, int capacity, string status);
    void MarkActive(Guid truckId, string truckName);
    void MarkInactive(Guid truckId);
    void RemoveAll();
    IReadOnlyCollection<TruckTelemetrySnapshot> GetAll();
}

public sealed record TruckTelemetrySnapshot(
    Guid TruckId,
    string TruckName,
    int CurrentLoad,
    int Capacity,
    string Status,
    bool IsActive);

public sealed class TruckTelemetryStore : ITruckTelemetryStore
{
    private readonly ConcurrentDictionary<Guid, TruckTelemetrySnapshot> _trucksById = new();

    public void Set(Guid truckId, string truckName, int currentLoad, int capacity, string status)
    {
        var sanitizedLoad = currentLoad < 0 ? 0 : currentLoad;
        var sanitizedCapacity = capacity < 0 ? 0 : capacity;
        _trucksById[truckId] = new TruckTelemetrySnapshot(truckId, truckName, sanitizedLoad, sanitizedCapacity, status, true);
    }

    public void MarkActive(Guid truckId, string truckName)
    {
        _trucksById.AddOrUpdate(
            truckId,
            _ => new TruckTelemetrySnapshot(truckId, truckName, 0, 0, "idle", true),
            (_, snapshot) => snapshot with { TruckName = truckName, IsActive = true });
    }

    public void MarkInactive(Guid truckId)
    {
        if (_trucksById.TryGetValue(truckId, out var snapshot))
        {
            _trucksById[truckId] = snapshot with
            {
                CurrentLoad = 0,
                Capacity = 0,
                Status = "inactive",
                IsActive = false
            };
        }
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