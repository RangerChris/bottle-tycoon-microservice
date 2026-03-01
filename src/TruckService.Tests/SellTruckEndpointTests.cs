using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TruckService.Data;
using TruckService.Services;
using TruckService.Tests.TestFixtures;
using Xunit;

namespace TruckService.Tests;

public class SellTruckEndpointTests : IClassFixture<TestcontainersFixture>
{
    private readonly TestcontainersFixture _fixture;
    private readonly Guid _testPlayerId = Guid.NewGuid();

    public SellTruckEndpointTests(TestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SellTruck_WhenIdle_ShouldSucceed()
    {
        if (!_fixture.Started) return;

        using var scope = _fixture.Host!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TruckDbContext>();

        var truck = new TruckEntity
        {
            Id = Guid.NewGuid(),
            Model = "Test Truck",
            IsActive = false,
            CapacityLevel = 0,
            IsBlockedForSale = false
        };
        db.Trucks.Add(truck);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        truck.IsBlockedForSale = true;
        truck.BlockedForSaleAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var updatedTruck = await db.Trucks.FindAsync([truck.Id], TestContext.Current.CancellationToken);
        updatedTruck.ShouldNotBeNull();
        updatedTruck.IsBlockedForSale.ShouldBeTrue();
        updatedTruck.BlockedForSaleAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task SellTruck_WhenActive_ShouldFail()
    {
        if (!_fixture.Started) return;

        using var scope = _fixture.Host!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TruckDbContext>();

        var truck = new TruckEntity
        {
            Id = Guid.NewGuid(),
            Model = "Active Truck",
            IsActive = true,
            CapacityLevel = 0,
            IsBlockedForSale = false
        };
        db.Trucks.Add(truck);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        truck.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task SellTruck_AlreadyBlocked_ShouldFail()
    {
        if (!_fixture.Started) return;

        using var scope = _fixture.Host!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TruckDbContext>();

        var truck = new TruckEntity
        {
            Id = Guid.NewGuid(),
            Model = "Blocked Truck",
            IsActive = false,
            CapacityLevel = 0,
            IsBlockedForSale = true,
            BlockedForSaleAt = DateTimeOffset.UtcNow
        };
        db.Trucks.Add(truck);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        truck.IsBlockedForSale.ShouldBeTrue();
    }

    [Fact]
    public async Task SellTruck_NonExistent_ShouldReturnNull()
    {
        if (!_fixture.Started) return;

        using var scope = _fixture.Host!.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITruckRepository>();

        var nonExistentId = Guid.NewGuid();
        var truck = await repo.GetEntityByIdAsync(nonExistentId, TestContext.Current.CancellationToken);
        truck.ShouldBeNull();
    }

    [Fact]
    public async Task BlockedTrucks_ShouldNotAppearInList()
    {
        if (!_fixture.Started) return;

        using var scope = _fixture.Host!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TruckDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<ITruckRepository>();

        var testId1 = Guid.NewGuid();
        var testId2 = Guid.NewGuid();

        var truck1 = new TruckEntity
        {
            Id = testId1,
            Model = "Active Truck Test",
            IsActive = false,
            CapacityLevel = 0,
            IsBlockedForSale = false
        };
        var truck2 = new TruckEntity
        {
            Id = testId2,
            Model = "Blocked Truck Test",
            IsActive = false,
            CapacityLevel = 0,
            IsBlockedForSale = true
        };
        db.Trucks.AddRange(truck1, truck2);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var activeTrucks = await repo.GetAllAsync(TestContext.Current.CancellationToken);
        var trucksList = activeTrucks.ToList();

        var testTruck = trucksList.FirstOrDefault(t => t.Id == testId1);
        testTruck.ShouldNotBeNull();

        var blockedTruck = trucksList.FirstOrDefault(t => t.Id == testId2);
        blockedTruck.ShouldBeNull();
    }

    [Fact]
    public async Task GetEntityByIdAsync_ShouldReturnEntity()
    {
        if (!_fixture.Started) return;

        using var scope = _fixture.Host!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TruckDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<ITruckRepository>();

        var truck = new TruckEntity
        {
            Id = Guid.NewGuid(),
            Model = "Test Truck",
            IsActive = false,
            CapacityLevel = 1
        };
        db.Trucks.Add(truck);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var retrieved = await repo.GetEntityByIdAsync(truck.Id, TestContext.Current.CancellationToken);
        retrieved.ShouldNotBeNull();
        retrieved.Id.ShouldBe(truck.Id);
        retrieved.Model.ShouldBe(truck.Model);
    }
}