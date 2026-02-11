using RecyclerService.Services;
using Shouldly;
using Xunit;

namespace RecyclerService.Tests.Unit;

public class RecyclerTelemetryStoreTests
{
    [Fact]
    public void Set_AddsSnapshot_CanRetrieveAll()
    {
        var store = new RecyclerTelemetryStore();
        var recyclerId = Guid.NewGuid();

        store.Set(recyclerId, "Test Recycler", 50, 2);

        var all = store.GetAll().ToArray();
        all.Length.ShouldBe(1);
        all.First().RecyclerId.ShouldBe(recyclerId);
        all.First().RecyclerName.ShouldBe("Test Recycler");
        all.First().CurrentBottles.ShouldBe(50);
        all.First().CurrentVisitors.ShouldBe(2);
    }

    [Fact]
    public void RemoveAll_ClearsStore()
    {
        var store = new RecyclerTelemetryStore();
        store.Set(Guid.NewGuid(), "Test Recycler", 100, 1);

        store.RemoveAll();

        store.GetAll().ToArray().ShouldBeEmpty();
    }

    [Fact]
    public void MultipleSnapshots_AreAllRetrievable()
    {
        var store = new RecyclerTelemetryStore();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        store.Set(id1, "Recycler 1", 50, 3);
        store.Set(id2, "Recycler 2", 75, 1);

        var all = store.GetAll().ToArray();
        all.Length.ShouldBe(2);
    }
}