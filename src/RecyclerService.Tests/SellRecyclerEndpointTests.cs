using System.Net;
using System.Net.Http.Json;
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
    [Fact]
    public async Task SellRecycler_WhenIdle_ShouldSucceed()
    {
        if (!fixture.Started)
        {
            return;
        }

        using var scope = fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
        var client = fixture.Client;

        var recycler = new Recycler
        {
            Id = Guid.NewGuid(),
            Name = "Idle Recycler",
            Capacity = 100,
            CapacityLevel = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            IsBlockedForSale = false
        };
        db.Recyclers.Add(recycler);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var req = new { PlayerId = Guid.NewGuid() };
        var res = await client.PostAsJsonAsync($"/recyclers/{recycler.Id}/sell", req, TestContext.Current.CancellationToken);

        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var verifyScope = fixture.Host.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
        var fetched = await verifyDb.Recyclers.FindAsync(new object[] { recycler.Id }, TestContext.Current.CancellationToken);
        fetched.ShouldBeNull();
    }

    [Fact]
    public async Task SellRecycler_WithWaitingCustomers_ShouldFail()
    {
        if (!fixture.Started)
        {
            return;
        }

        using var scope = fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
        var client = fixture.Client;

        var recycler = new Recycler
        {
            Id = Guid.NewGuid(),
            Name = "Busy Recycler",
            Capacity = 100,
            CapacityLevel = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            IsBlockedForSale = false
        };

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            RecyclerId = recycler.Id,
            Status = CustomerStatus.Waiting,
            ArrivedAt = DateTimeOffset.UtcNow
        };

        recycler.Customers.Add(customer);
        db.Recyclers.Add(recycler);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var req = new { PlayerId = Guid.NewGuid() };
        var res = await client.PostAsJsonAsync($"/recyclers/{recycler.Id}/sell", req, TestContext.Current.CancellationToken);

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var verifyScope = fixture.Host.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
        var fetched = await verifyDb.Recyclers.FindAsync(new object[] { recycler.Id }, TestContext.Current.CancellationToken);
        fetched.ShouldNotBeNull();
        fetched.IsBlockedForSale.ShouldBeFalse();
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
        var client = fixture.Client;

        var recycler = new Recycler
        {
            Id = Guid.NewGuid(),
            Name = "Already Blocked",
            Capacity = 100,
            CapacityLevel = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            IsBlockedForSale = true,
            BlockedForSaleAt = DateTimeOffset.UtcNow
        };
        db.Recyclers.Add(recycler);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var req = new { PlayerId = Guid.NewGuid() };
        var res = await client.PostAsJsonAsync($"/recyclers/{recycler.Id}/sell", req, TestContext.Current.CancellationToken);

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var verifyScope = fixture.Host.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
        var fetched = await verifyDb.Recyclers.FindAsync(new object[] { recycler.Id }, TestContext.Current.CancellationToken);
        fetched.ShouldNotBeNull();
        fetched.IsBlockedForSale.ShouldBeTrue();
    }

    [Fact]
    public async Task SellRecycler_NonExistent_ShouldReturnNotFound()
    {
        if (!fixture.Started)
        {
            return;
        }

        var client = fixture.Client;
        var req = new { PlayerId = Guid.NewGuid() };
        var res = await client.PostAsJsonAsync($"/recyclers/{Guid.NewGuid()}/sell", req, TestContext.Current.CancellationToken);

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SellRecycler_WithProcessingCustomer_ShouldFail()
    {
        if (!fixture.Started)
        {
            return;
        }

        using var scope = fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
        var client = fixture.Client;

        var recycler = new Recycler
        {
            Id = Guid.NewGuid(),
            Name = "Recycler With Processing Customer",
            Capacity = 100,
            CapacityLevel = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            IsBlockedForSale = false
        };

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            RecyclerId = recycler.Id,
            Status = CustomerStatus.Processing,
            ArrivedAt = DateTimeOffset.UtcNow
        };

        recycler.Customers.Add(customer);
        db.Recyclers.Add(recycler);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var req = new { PlayerId = Guid.NewGuid() };
        var res = await client.PostAsJsonAsync($"/recyclers/{recycler.Id}/sell", req, TestContext.Current.CancellationToken);

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SellRecycler_SuccessfulSale_RemovesRecyclerAndCreditsPlayer()
    {
        if (!fixture.Started)
        {
            return;
        }

        using var scope = fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
        var client = fixture.Client;

        var recycler = new Recycler
        {
            Id = Guid.NewGuid(),
            Name = "Sale Ready Recycler",
            Capacity = 100,
            CapacityLevel = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            IsBlockedForSale = false
        };
        db.Recyclers.Add(recycler);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var playerId = Guid.NewGuid();
        var req = new { PlayerId = playerId };
        var res = await client.PostAsJsonAsync($"/recyclers/{recycler.Id}/sell", req, TestContext.Current.CancellationToken);

        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var verifyScope = fixture.Host.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
        var fetched = await verifyDb.Recyclers.FindAsync(new object[] { recycler.Id }, TestContext.Current.CancellationToken);
        fetched.ShouldBeNull();
    }

    [Fact]
    public async Task SellRecycler_WithMultipleCustomers_ShouldFail()
    {
        if (!fixture.Started)
        {
            return;
        }

        using var scope = fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
        var client = fixture.Client;

        var recycler = new Recycler
        {
            Id = Guid.NewGuid(),
            Name = "Busy Recycler Multiple Customers",
            Capacity = 100,
            CapacityLevel = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            IsBlockedForSale = false
        };

        var customer1 = new Customer
        {
            Id = Guid.NewGuid(),
            RecyclerId = recycler.Id,
            Status = CustomerStatus.Waiting,
            ArrivedAt = DateTimeOffset.UtcNow
        };

        var customer2 = new Customer
        {
            Id = Guid.NewGuid(),
            RecyclerId = recycler.Id,
            Status = CustomerStatus.Done,
            ArrivedAt = DateTimeOffset.UtcNow
        };

        recycler.Customers.Add(customer1);
        recycler.Customers.Add(customer2);
        db.Recyclers.Add(recycler);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var req = new { PlayerId = Guid.NewGuid() };
        var res = await client.PostAsJsonAsync($"/recyclers/{recycler.Id}/sell", req, TestContext.Current.CancellationToken);

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var verifyScope = fixture.Host.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
        var fetched = await verifyDb.Recyclers.FindAsync(new object[] { recycler.Id }, TestContext.Current.CancellationToken);
        fetched.ShouldNotBeNull();
    }
}