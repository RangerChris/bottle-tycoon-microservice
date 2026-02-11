using System.Collections.Concurrent;

namespace RecyclerService.Services;

public interface IRecyclerTelemetryStore
{
    void Set(Guid recyclerId, string recyclerName, int currentBottles, int currentVisitors);
    void RemoveAll();
    IReadOnlyCollection<RecyclerTelemetrySnapshot> GetAll();
}

public sealed record RecyclerTelemetrySnapshot(Guid RecyclerId, string RecyclerName, int CurrentBottles, int CurrentVisitors);

public sealed class RecyclerTelemetryStore : IRecyclerTelemetryStore
{
    private readonly ConcurrentDictionary<Guid, RecyclerTelemetrySnapshot> _recyclerById = new();

    public void Set(Guid recyclerId, string recyclerName, int currentBottles, int currentVisitors)
    {
        var sanitizedBottles = currentBottles < 0 ? 0 : currentBottles;
        var sanitizedVisitors = currentVisitors < 0 ? 0 : currentVisitors;
        _recyclerById[recyclerId] = new RecyclerTelemetrySnapshot(recyclerId, recyclerName, sanitizedBottles, sanitizedVisitors);
    }

    public void RemoveAll()
    {
        _recyclerById.Clear();
    }

    public IReadOnlyCollection<RecyclerTelemetrySnapshot> GetAll()
    {
        return _recyclerById.Values.ToArray();
    }
}