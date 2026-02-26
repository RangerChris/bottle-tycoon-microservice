using GameService.Services;
using Shouldly;
using Xunit;

namespace GameService.Tests.Unit;

public class GameTelemetryStoreTests
{
    [Fact]
    public void SetTotalEarnings_StoresSnapshot()
    {
        var store = new GameTelemetryStore();
        var playerId = Guid.NewGuid();
        var earnings = 1000m;

        store.SetTotalEarnings(playerId, earnings);

        var snapshots = store.GetAll();
        snapshots.Count.ShouldBe(1);
        var snapshot = snapshots.First();
        snapshot.PlayerId.ShouldBe(playerId);
        snapshot.TotalEarnings.ShouldBe(earnings);
    }

    [Fact]
    public void SetTotalEarnings_WithNegativeValue_StoresZero()
    {
        var store = new GameTelemetryStore();
        var playerId = Guid.NewGuid();

        store.SetTotalEarnings(playerId, -500m);

        var snapshots = store.GetAll();
        var snapshot = snapshots.First();
        snapshot.TotalEarnings.ShouldBe(0m);
    }

    [Fact]
    public void SetTotalEarnings_UpdatesExistingPlayer()
    {
        var store = new GameTelemetryStore();
        var playerId = Guid.NewGuid();

        store.SetTotalEarnings(playerId, 1000m);
        store.SetTotalEarnings(playerId, 1500m);

        var snapshots = store.GetAll();
        snapshots.Count.ShouldBe(1);
        snapshots.First().TotalEarnings.ShouldBe(1500m);
    }

    [Fact]
    public void RemoveAll_ClearsAllSnapshots()
    {
        var store = new GameTelemetryStore();
        store.SetTotalEarnings(Guid.NewGuid(), 1000m);
        store.SetTotalEarnings(Guid.NewGuid(), 2000m);

        store.RemoveAll();

        var snapshots = store.GetAll();
        snapshots.Count.ShouldBe(0);
    }

    [Fact]
    public void GetAll_ReturnsEmptyCollection_WhenNoSnapshots()
    {
        var store = new GameTelemetryStore();

        var snapshots = store.GetAll();

        snapshots.ShouldNotBeNull();
        snapshots.Count.ShouldBe(0);
    }

    [Fact]
    public void GetAll_ReturnsAllSnapshots()
    {
        var store = new GameTelemetryStore();
        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();
        var player3 = Guid.NewGuid();

        store.SetTotalEarnings(player1, 1000m);
        store.SetTotalEarnings(player2, 2000m);
        store.SetTotalEarnings(player3, 3000m);

        var snapshots = store.GetAll();

        snapshots.Count.ShouldBe(3);
    }

    [Fact]
    public void SetTotalEarnings_WithZero_Stores()
    {
        var store = new GameTelemetryStore();
        var playerId = Guid.NewGuid();

        store.SetTotalEarnings(playerId, 0m);

        var snapshots = store.GetAll();
        snapshots.First().TotalEarnings.ShouldBe(0m);
    }

    [Fact]
    public void SetTotalEarnings_WithLargeValue_Stores()
    {
        var store = new GameTelemetryStore();
        var playerId = Guid.NewGuid();
        var largeValue = 999999999.99m;

        store.SetTotalEarnings(playerId, largeValue);

        var snapshots = store.GetAll();
        snapshots.First().TotalEarnings.ShouldBe(largeValue);
    }
}