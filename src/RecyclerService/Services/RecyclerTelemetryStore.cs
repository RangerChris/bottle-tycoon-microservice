using System.Collections.Concurrent;

namespace RecyclerService.Services;

public interface IRecyclerTelemetryStore
{
    void Set(Guid recyclerId, int currentBottles);
    void RemoveAll();
    IReadOnlyCollection<RecyclerTelemetrySnapshot> GetAll();
}

public sealed record RecyclerTelemetrySnapshot(Guid RecyclerId, int CurrentBottles);

public sealed class RecyclerTelemetryStore : IRecyclerTelemetryStore
{
    private readonly ConcurrentDictionary<Guid, int> _bottlesByRecycler = new();

    public void Set(Guid recyclerId, int currentBottles)
    {
        var sanitized = currentBottles < 0 ? 0 : currentBottles;
        _bottlesByRecycler[recyclerId] = sanitized;
    }

    public void RemoveAll()
    {
        _bottlesByRecycler.Clear();
    }

    public IReadOnlyCollection<RecyclerTelemetrySnapshot> GetAll()
    {
        return _bottlesByRecycler.Select(kv => new RecyclerTelemetrySnapshot(kv.Key, kv.Value)).ToArray();
    }
}