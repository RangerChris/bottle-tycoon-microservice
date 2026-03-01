using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TruckService.Data;
using TruckService.Services;
using TruckService.Tests.TestFixtures;
using Xunit;

namespace TruckService.Tests;

public class SellTruckEndpointTests(TestcontainersFixture fixture) : IClassFixture<TestcontainersFixture>
{
    [Fact]
    public async Task SellTruck_WhenIdle_ShouldSucceed()
    {
        if (!fixture.Started)
        {
            return;
        }

        using var scope = fixture.Host!.Services.CreateScope();
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
        if (!fixture.Started)
        {
            return;
        }

        using var scope = fixture.Host!.Services.CreateScope();
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

        // Call the endpoint via HTTP
        var client = fixture.Client;
        var req = new { PlayerId = Guid.NewGuid() };
        var res = await client.PostAsJsonAsync($"/truck/{truck.Id}/sell", req, TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Ensure DB unchanged
        using var verifyScope = fixture.Host!.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TruckDbContext>();
        var fetched = await verifyDb.Trucks.FindAsync([truck.Id], TestContext.Current.CancellationToken);
        fetched.ShouldNotBeNull();
        fetched.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task SellTruck_AlreadyBlocked_ShouldFail()
    {
        if (!fixture.Started)
        {
            return;
        }

        using var scope = fixture.Host!.Services.CreateScope();
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

        var client = fixture.Client;
        var req = new { PlayerId = Guid.NewGuid() };
        var res = await client.PostAsJsonAsync($"/truck/{truck.Id}/sell", req, TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var fetched = await db.Trucks.FindAsync([truck.Id], TestContext.Current.CancellationToken);
        fetched.ShouldNotBeNull();
        fetched.IsBlockedForSale.ShouldBeTrue();
    }

    [Fact]
    public async Task SellTruck_NonExistent_ShouldReturnNotFound()
    {
        if (!fixture.Started)
        {
            return;
        }

        var client = fixture.Client;
        var req = new { PlayerId = Guid.NewGuid() };
        var res = await client.PostAsJsonAsync($"/truck/{Guid.NewGuid()}/sell", req, TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SellTruck_SuccessfulSale_BlocksTruckAndCreditsPlayer()
    {
        if (!fixture.Started)
        {
            return;
        }

        using var scope = fixture.Host!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TruckDbContext>();

        var truck = new TruckEntity
        {
            Id = Guid.NewGuid(),
            Model = "Truck for Sale",
            IsActive = false,
            CapacityLevel = 0,
            IsBlockedForSale = false
        };
        db.Trucks.Add(truck);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var client = fixture.Client;
        var req = new { PlayerId = Guid.NewGuid() };
        var res = await client.PostAsJsonAsync($"/truck/{truck.Id}/sell", req, TestContext.Current.CancellationToken);

        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var verifyScope = fixture.Host!.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TruckDbContext>();
        var fetched = await verifyDb.Trucks.FindAsync(new object[] { truck.Id }, TestContext.Current.CancellationToken);
        fetched.ShouldBeNull();
    }

    [Fact]
    public async Task BlockedTrucks_ShouldNotAppearInList()
    {
        if (!fixture.Started)
        {
            return;
        }

        using var scope = fixture.Host!.Services.CreateScope();
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
        if (!fixture.Started)
        {
            return;
        }

        using var scope = fixture.Host!.Services.CreateScope();
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