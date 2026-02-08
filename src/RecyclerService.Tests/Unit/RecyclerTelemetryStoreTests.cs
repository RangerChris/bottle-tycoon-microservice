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

        store.Set(recyclerId, 50);

        var all = store.GetAll().ToArray();
        all.Length.ShouldBe(1);
        all.First().RecyclerId.ShouldBe(recyclerId);
        all.First().CurrentBottles.ShouldBe(50);
    }

    [Fact]
    public void RemoveAll_ClearsStore()
    {
        var store = new RecyclerTelemetryStore();
        store.Set(Guid.NewGuid(), 100);

        store.RemoveAll();

        store.GetAll().ToArray().ShouldBeEmpty();
    }

    [Fact]
    public void MultipleSnapshots_AreAllRetrievable()
    {
        var store = new RecyclerTelemetryStore();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        store.Set(id1, 50);
        store.Set(id2, 75);

        var all = store.GetAll().ToArray();
        all.Length.ShouldBe(2);
    }
}