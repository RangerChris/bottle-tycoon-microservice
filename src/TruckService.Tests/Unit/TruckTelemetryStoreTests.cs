using Shouldly;
using TruckService.Services;
using Xunit;

namespace TruckService.Tests.Unit;

public class TruckTelemetryStoreTests
{
    [Fact]
    public void Set_StoresSnapshot()
    {
        var store = new TruckTelemetryStore();
        var truckId = Guid.NewGuid();
        var truckName = "Truck 1";
        var currentLoad = 50;
        var capacity = 100;
        var status = "Idle";

        store.Set(truckId, truckName, currentLoad, capacity, status);

        var snapshots = store.GetAll();
        snapshots.Count.ShouldBe(1);
        var snapshot = snapshots.First();
        snapshot.TruckId.ShouldBe(truckId);
        snapshot.TruckName.ShouldBe(truckName);
        snapshot.CurrentLoad.ShouldBe(currentLoad);
        snapshot.Capacity.ShouldBe(capacity);
        snapshot.Status.ShouldBe(status);
    }

    [Fact]
    public void Set_WithNegativeLoad_StoresZero()
    {
        var store = new TruckTelemetryStore();
        var truckId = Guid.NewGuid();

        store.Set(truckId, "Truck 1", -10, 100, "Idle");

        var snapshot = store.GetAll().First();
        snapshot.CurrentLoad.ShouldBe(0);
    }

    [Fact]
    public void Set_WithNegativeCapacity_StoresZero()
    {
        var store = new TruckTelemetryStore();
        var truckId = Guid.NewGuid();

        store.Set(truckId, "Truck 1", 50, -100, "Idle");

        var snapshot = store.GetAll().First();
        snapshot.Capacity.ShouldBe(0);
    }

    [Fact]
    public void Set_UpdatesExistingTruck()
    {
        var store = new TruckTelemetryStore();
        var truckId = Guid.NewGuid();

        store.Set(truckId, "Truck 1", 50, 100, "Idle");
        store.Set(truckId, "Truck 1", 75, 100, "Loading");

        var snapshots = store.GetAll();
        snapshots.Count.ShouldBe(1);
        var snapshot = snapshots.First();
        snapshot.CurrentLoad.ShouldBe(75);
        snapshot.Status.ShouldBe("Loading");
    }

    [Fact]
    public void RemoveAll_ClearsAllSnapshots()
    {
        var store = new TruckTelemetryStore();
        store.Set(Guid.NewGuid(), "Truck 1", 50, 100, "Idle");
        store.Set(Guid.NewGuid(), "Truck 2", 30, 80, "Traveling");

        store.RemoveAll();

        var snapshots = store.GetAll();
        snapshots.Count.ShouldBe(0);
    }

    [Fact]
    public void GetAll_ReturnsEmptyCollection_WhenNoSnapshots()
    {
        var store = new TruckTelemetryStore();

        var snapshots = store.GetAll();

        snapshots.ShouldNotBeNull();
        snapshots.Count.ShouldBe(0);
    }

    [Fact]
    public void GetAll_ReturnsAllSnapshots()
    {
        var store = new TruckTelemetryStore();
        var truck1 = Guid.NewGuid();
        var truck2 = Guid.NewGuid();
        var truck3 = Guid.NewGuid();

        store.Set(truck1, "Truck 1", 50, 100, "Idle");
        store.Set(truck2, "Truck 2", 30, 80, "Traveling");
        store.Set(truck3, "Truck 3", 60, 120, "Loading");

        var snapshots = store.GetAll();

        snapshots.Count.ShouldBe(3);
    }

    [Fact]
    public void Set_WithZeroValues_Stores()
    {
        var store = new TruckTelemetryStore();
        var truckId = Guid.NewGuid();

        store.Set(truckId, "Truck 1", 0, 0, "Empty");

        var snapshot = store.GetAll().First();
        snapshot.CurrentLoad.ShouldBe(0);
        snapshot.Capacity.ShouldBe(0);
    }

    [Fact]
    public void Set_WithEmptyTruckName_Stores()
    {
        var store = new TruckTelemetryStore();
        var truckId = Guid.NewGuid();

        store.Set(truckId, "", 50, 100, "Idle");

        var snapshot = store.GetAll().First();
        snapshot.TruckName.ShouldBe("");
    }

    [Fact]
    public void Set_MarksSnapshotAsActive()
    {
        var store = new TruckTelemetryStore();
        var truckId = Guid.NewGuid();

        store.Set(truckId, "Truck 1", 10, 45, "idle");

        var snapshot = store.GetAll().Single();
        snapshot.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void MarkInactive_KeepsSnapshotAndMarksInactive()
    {
        var store = new TruckTelemetryStore();
        var truckId = Guid.NewGuid();

        store.Set(truckId, "Truck 1", 10, 45, "loading");
        store.MarkInactive(truckId);

        var snapshot = store.GetAll().Single();
        snapshot.IsActive.ShouldBeFalse();
        snapshot.CurrentLoad.ShouldBe(0);
        snapshot.Capacity.ShouldBe(0);
        snapshot.Status.ShouldBe("inactive");
    }

    [Fact]
    public void MarkActive_RestoresInactiveSnapshotToActive()
    {
        var store = new TruckTelemetryStore();
        var truckId = Guid.NewGuid();

        store.Set(truckId, "Truck 1", 5, 45, "idle");
        store.MarkInactive(truckId);
        store.MarkActive(truckId, "Truck 1");

        var snapshot = store.GetAll().Single();
        snapshot.IsActive.ShouldBeTrue();
    }
}