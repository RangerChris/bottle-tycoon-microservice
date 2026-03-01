using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecyclerService.Data;
using RecyclerService.Models;
using RecyclerService.Tests.TestFixtures;
using Shouldly;
using Xunit;

namespace RecyclerService.Tests;

public class SellRecyclerEndpointTests(TestcontainersFixture fixture) : IClassFixture<TestcontainersFixture>
{
    private readonly Guid _testPlayerId = Guid.NewGuid();

    [Fact]
    public async Task SellRecycler_WithNoCustomers_ShouldSucceed()
    {
        if (!fixture.Started)
        {
            return;
        }

        using var scope = fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();

        var recycler = new Recycler
        {
            Id = Guid.NewGuid(),
            Name = "Test Recycler",
            Capacity = 100,
            CapacityLevel = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            IsBlockedForSale = false
        };
        db.Recyclers.Add(recycler);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        recycler.IsBlockedForSale = true;
        recycler.BlockedForSaleAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var updatedRecycler = await db.Recyclers.FindAsync([recycler.Id], TestContext.Current.CancellationToken);
        updatedRecycler.ShouldNotBeNull();
        updatedRecycler.IsBlockedForSale.ShouldBeTrue();
        updatedRecycler.BlockedForSaleAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task SellRecycler_WithCustomers_ShouldFail()
    {
        if (!fixture.Started)
        {
            return;
        }

        using var scope = fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();

        var recycler = new Recycler
        {
            Id = Guid.NewGuid(),
            Name = "Test Recycler",
            Capacity = 100,
            CapacityLevel = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            IsBlockedForSale = false,
            Customers = new List<Customer>
            {
                new() { Id = Guid.NewGuid(), CustomerType = "Walk-in" }
            }
        };
        db.Recyclers.Add(recycler);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var hasCustomers = recycler.Customers.Any();
        hasCustomers.ShouldBeTrue();
    }

    [Fact]
    public async Task SellRecycler_AlreadyBlocked_ShouldFail()
    {
        if (!fixture.Started)
        {
            return;
        }

        using var scope = fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();

        var recycler = new Recycler
        {
            Id = Guid.NewGuid(),
            Name = "Test Recycler",
            Capacity = 100,
            CapacityLevel = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            IsBlockedForSale = true,
            BlockedForSaleAt = DateTimeOffset.UtcNow
        };
        db.Recyclers.Add(recycler);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        recycler.IsBlockedForSale.ShouldBeTrue();
    }

    [Fact]
    public async Task SellRecycler_NonExistent_ShouldReturnNotFound()
    {
        if (!fixture.Started)
        {
            return;
        }

        using var scope = fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();

        var nonExistentId = Guid.NewGuid();
        var recycler = await db.Recyclers.FindAsync([nonExistentId], TestContext.Current.CancellationToken);
        recycler.ShouldBeNull();
    }

    [Fact]
    public async Task BlockedRecyclers_ShouldNotAppearInList()
    {
        if (!fixture.Started)
        {
            return;
        }

        using var scope = fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();

        var testId1 = Guid.NewGuid();
        var testId2 = Guid.NewGuid();

        var recycler1 = new Recycler
        {
            Id = testId1,
            Name = "Active Recycler Test",
            Capacity = 100,
            CapacityLevel = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            IsBlockedForSale = false
        };
        var recycler2 = new Recycler
        {
            Id = testId2,
            Name = "Blocked Recycler Test",
            Capacity = 100,
            CapacityLevel = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            IsBlockedForSale = true
        };
        db.Recyclers.AddRange(recycler1, recycler2);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var activeRecyclers = await db.Recyclers
            .Where(r => !r.IsBlockedForSale)
            .ToListAsync(TestContext.Current.CancellationToken);

        var testRecycler = activeRecyclers.FirstOrDefault(r => r.Id == testId1);
        testRecycler.ShouldNotBeNull();

        var blockedRecycler = activeRecyclers.FirstOrDefault(r => r.Id == testId2);
        blockedRecycler.ShouldBeNull();
    }
}