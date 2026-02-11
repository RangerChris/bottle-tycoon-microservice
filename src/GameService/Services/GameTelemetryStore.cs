using System.Collections.Concurrent;

namespace GameService.Services;

public interface IGameTelemetryStore
{
    void SetTotalEarnings(Guid playerId, decimal totalEarnings);
    void RemoveAll();
    IReadOnlyCollection<GameTelemetrySnapshot> GetAll();
}

public sealed record GameTelemetrySnapshot(Guid PlayerId, decimal TotalEarnings);

public sealed class GameTelemetryStore : IGameTelemetryStore
{
    private readonly ConcurrentDictionary<Guid, GameTelemetrySnapshot> _playerById = new();

    public void SetTotalEarnings(Guid playerId, decimal totalEarnings)
    {
        var sanitized = totalEarnings < 0 ? 0 : totalEarnings;
        _playerById[playerId] = new GameTelemetrySnapshot(playerId, sanitized);
    }

    public void RemoveAll()
    {
        _playerById.Clear();
    }

    public IReadOnlyCollection<GameTelemetrySnapshot> GetAll()
    {
        return _playerById.Values.ToArray();
    }
}