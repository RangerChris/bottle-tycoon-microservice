using System.Collections.Concurrent;

namespace RecyclerService.Services;

public interface IRecyclerTelemetryStore
{
    void Set(Guid recyclerId, string recyclerName, int currentBottles, int currentVisitors, int queueDepth);
    void RemoveAll();
    IReadOnlyCollection<RecyclerTelemetrySnapshot> GetAll();
}

public sealed record RecyclerTelemetrySnapshot(Guid RecyclerId, string RecyclerName, int CurrentBottles, int CurrentVisitors, int QueueDepth);

public sealed class RecyclerTelemetryStore : IRecyclerTelemetryStore
{
    private readonly ConcurrentDictionary<Guid, RecyclerTelemetrySnapshot> _recyclerById = new();

    public void Set(Guid recyclerId, string recyclerName, int currentBottles, int currentVisitors, int queueDepth)
    {
        var sanitizedBottles = currentBottles < 0 ? 0 : currentBottles;
        var sanitizedVisitors = currentVisitors < 0 ? 0 : currentVisitors;
        var sanitizedQueueDepth = queueDepth < 0 ? 0 : queueDepth;
        _recyclerById[recyclerId] = new RecyclerTelemetrySnapshot(recyclerId, recyclerName, sanitizedBottles, sanitizedVisitors, sanitizedQueueDepth);
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