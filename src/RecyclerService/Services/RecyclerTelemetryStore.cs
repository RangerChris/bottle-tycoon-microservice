using System.Collections.Concurrent;

namespace RecyclerService.Services;

public interface IRecyclerTelemetryStore
{
    void Set(Guid recyclerId, string recyclerName, int currentBottles, int currentVisitors, int queueDepth);
    void MarkActive(Guid recyclerId, string recyclerName);
    void MarkInactive(Guid recyclerId);
    void RemoveAll();
    IReadOnlyCollection<RecyclerTelemetrySnapshot> GetAll();
}

public sealed record RecyclerTelemetrySnapshot(
    Guid RecyclerId,
    string RecyclerName,
    int CurrentBottles,
    int CurrentVisitors,
    int QueueDepth,
    bool IsActive);

public sealed class RecyclerTelemetryStore : IRecyclerTelemetryStore
{
    private readonly ConcurrentDictionary<Guid, RecyclerTelemetrySnapshot> _recyclerById = new();

    public void Set(Guid recyclerId, string recyclerName, int currentBottles, int currentVisitors, int queueDepth)
    {
        var sanitizedBottles = currentBottles < 0 ? 0 : currentBottles;
        var sanitizedVisitors = currentVisitors < 0 ? 0 : currentVisitors;
        var sanitizedQueueDepth = queueDepth < 0 ? 0 : queueDepth;
        _recyclerById[recyclerId] = new RecyclerTelemetrySnapshot(
            recyclerId,
            recyclerName,
            sanitizedBottles,
            sanitizedVisitors,
            sanitizedQueueDepth,
            true);
    }

    public void MarkActive(Guid recyclerId, string recyclerName)
    {
        _recyclerById.AddOrUpdate(
            recyclerId,
            _ => new RecyclerTelemetrySnapshot(recyclerId, recyclerName, 0, 0, 0, true),
            (_, snapshot) => snapshot with { RecyclerName = recyclerName, IsActive = true });
    }

    public void MarkInactive(Guid recyclerId)
    {
        if (_recyclerById.TryGetValue(recyclerId, out var snapshot))
        {
            _recyclerById[recyclerId] = snapshot with
            {
                CurrentBottles = 0,
                CurrentVisitors = 0,
                QueueDepth = 0,
                IsActive = false
            };
        }
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