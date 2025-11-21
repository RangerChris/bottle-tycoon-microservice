using HeadquartersService.Models;
using HeadquartersService.Services;
using Shouldly;
using Xunit;

namespace HeadquartersService.Tests;

public class DispatchLogicTests
{
    [Fact]
    public void PriorityCalculation_IsCorrect()
    {
        var req = new DispatchRequest { FullnessPercentage = 90 };
        var q = new DispatchQueue();
        q.Enqueue(req);
        var peek = q.PeekAll().First();
        Assert.InRange(peek.Priority, 9.0 - 0.0001, 9.0 + 0.0001);
    }

    [Fact]
    public void Queue_Sorts_ByPriorityThenFifo()
    {
        var q = new DispatchQueue();
        var first = new DispatchRequest { FullnessPercentage = 95 };
        var second = new DispatchRequest { FullnessPercentage = 95 };
        var third = new DispatchRequest { FullnessPercentage = 80 };

        q.Enqueue(first);
        Thread.Sleep(5);
        q.Enqueue(second);
        Thread.Sleep(5);
        q.Enqueue(third);

        var all = q.PeekAll().ToArray();
        all[0].Id.ShouldBe(first.Id);
        all[1].Id.ShouldBe(second.Id);
        all[2].Id.ShouldBe(third.Id);
    }

    [Fact]
    public void Fleet_Assignment_PicksSmallestFittingTruck()
    {
        var fleet = new FleetService();
        var small = new Truck { Capacity = 50, Reliability = 0.9 };
        var medium = new Truck { Capacity = 100, Reliability = 0.95 };
        var large = new Truck { Capacity = 150, Reliability = 0.8 };
        fleet.AddTruck(small);
        fleet.AddTruck(medium);
        fleet.AddTruck(large);

        var req = new DispatchRequest { ExpectedBottles = 90, FullnessPercentage = 95 };
        var assigned = fleet.TryAssignTruck(req, out var truck);
        assigned.ShouldBeTrue();
        truck.ShouldNotBeNull();
        truck.Capacity.ShouldBe(100);
        req.AssignedTruckId.ShouldBe(truck.Id);
    }

    [Fact]
    public void NoAvailableTruck_ReturnsFalse()
    {
        var fleet = new FleetService();
        var truck = new Truck { Capacity = 50 };
        fleet.AddTruck(truck);
        truck.Status = TruckStatus.InProgress; // not idle

        var req = new DispatchRequest { ExpectedBottles = 40 };
        var ok = fleet.TryAssignTruck(req, out var t);
        ok.ShouldBeFalse();
        t.ShouldBeNull();
    }
}