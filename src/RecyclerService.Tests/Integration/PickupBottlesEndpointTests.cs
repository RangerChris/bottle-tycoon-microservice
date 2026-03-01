using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RecyclerService.Data;
using RecyclerService.Models;
using RecyclerService.Tests.TestFixtures;
using Shouldly;
using Xunit;

namespace RecyclerService.Tests.Integration;

public class PickupBottlesEndpointTests(TestcontainersFixture fixture) : IClassFixture<TestcontainersFixture>
{
    [Fact]
    public async Task PickupBottles_WithValidRecycler_ReturnsPickedUpBottles()
    {
        var client = fixture.Client;
        Guid recyclerId;

        using (var scope = fixture.Host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
            var recycler = new Recycler
            {
                Id = Guid.NewGuid(),
                Name = "Test Recycler",
                Capacity = 100,
                Location = "Test Location",
                CreatedAt = DateTimeOffset.UtcNow
            };
            recycler.SetBottleInventory(new Dictionary<string, int>
            {
                { "glass", 10 },
                { "metal", 15 },
                { "plastic", 20 }
            });
            db.Recyclers.Add(recycler);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            recyclerId = recycler.Id;
        }

        var request = new { RecyclerId = recyclerId, MaxCapacity = 50 };
        var response = await client.PostAsJsonAsync($"/recyclers/{recyclerId}/pickup", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PickupBottlesResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.TotalPickedUp.ShouldBe(45);
        body.BottlesPickedUp["glass"].ShouldBe(10);
        body.BottlesPickedUp["metal"].ShouldBe(15);
        body.BottlesPickedUp["plastic"].ShouldBe(20);
    }

    [Fact]
    public async Task PickupBottles_WithCapacityLimit_RespectsMaxCapacity()
    {
        var client = fixture.Client;
        Guid recyclerId;

        using (var scope = fixture.Host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
            var recycler = new Recycler
            {
                Id = Guid.NewGuid(),
                Name = "Test Recycler",
                Capacity = 100,
                Location = "Test Location",
                CreatedAt = DateTimeOffset.UtcNow
            };
            recycler.SetBottleInventory(new Dictionary<string, int>
            {
                { "glass", 30 },
                { "metal", 30 },
                { "plastic", 30 }
            });
            db.Recyclers.Add(recycler);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            recyclerId = recycler.Id;
        }

        var request = new { RecyclerId = recyclerId, MaxCapacity = 25 };
        var response = await client.PostAsJsonAsync($"/recyclers/{recyclerId}/pickup", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PickupBottlesResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.TotalPickedUp.ShouldBe(25);
    }

    [Fact]
    public async Task PickupBottles_WithNonExistentRecycler_Returns404()
    {
        var client = fixture.Client;
        var nonExistentId = Guid.NewGuid();

        var request = new { RecyclerId = nonExistentId, MaxCapacity = 50 };
        var response = await client.PostAsJsonAsync($"/recyclers/{nonExistentId}/pickup", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PickupBottles_WithEmptyRecycler_ReturnsEmptyPickup()
    {
        var client = fixture.Client;
        Guid recyclerId;

        using (var scope = fixture.Host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
            var recycler = new Recycler
            {
                Id = Guid.NewGuid(),
                Name = "Test Recycler",
                Capacity = 100,
                Location = "Test Location",
                CreatedAt = DateTimeOffset.UtcNow
            };
            recycler.SetBottleInventory(new Dictionary<string, int>
            {
                { "glass", 0 },
                { "metal", 0 },
                { "plastic", 0 }
            });
            db.Recyclers.Add(recycler);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            recyclerId = recycler.Id;
        }

        var request = new { RecyclerId = recyclerId, MaxCapacity = 50 };
        var response = await client.PostAsJsonAsync($"/recyclers/{recyclerId}/pickup", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PickupBottlesResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.TotalPickedUp.ShouldBe(0);
    }

    [Fact]
    public async Task PickupBottles_UpdatesRecyclerInventory()
    {
        var client = fixture.Client;
        Guid recyclerId;

        using (var scope = fixture.Host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
            var recycler = new Recycler
            {
                Id = Guid.NewGuid(),
                Name = "Test Recycler",
                Capacity = 100,
                Location = "Test Location",
                CreatedAt = DateTimeOffset.UtcNow
            };
            recycler.SetBottleInventory(new Dictionary<string, int>
            {
                { "glass", 20 },
                { "metal", 20 },
                { "plastic", 20 }
            });
            db.Recyclers.Add(recycler);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            recyclerId = recycler.Id;
        }

        var request = new { RecyclerId = recyclerId, MaxCapacity = 30 };
        await client.PostAsJsonAsync($"/recyclers/{recyclerId}/pickup", request, TestContext.Current.CancellationToken);

        using (var scope = fixture.Host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
            var recycler = await db.Recyclers.FindAsync([recyclerId], TestContext.Current.CancellationToken);
            recycler.ShouldNotBeNull();
            var inventory = recycler.GetBottleInventory();
            inventory["glass"].ShouldBe(0);
            inventory["metal"].ShouldBe(10);
            inventory["plastic"].ShouldBe(20);
        }
    }

    [Fact]
    public async Task PickupBottles_PartialPickup_ReturnsCorrectRemaining()
    {
        var client = fixture.Client;
        Guid recyclerId;

        using (var scope = fixture.Host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
            var recycler = new Recycler
            {
                Id = Guid.NewGuid(),
                Name = "Test Recycler",
                Capacity = 100,
                Location = "Test Location",
                CreatedAt = DateTimeOffset.UtcNow
            };
            recycler.SetBottleInventory(new Dictionary<string, int>
            {
                { "glass", 50 },
                { "metal", 50 },
                { "plastic", 50 }
            });
            db.Recyclers.Add(recycler);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            recyclerId = recycler.Id;
        }

        var request = new { RecyclerId = recyclerId, MaxCapacity = 100 };
        var response = await client.PostAsJsonAsync($"/recyclers/{recyclerId}/pickup", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PickupBottlesResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.TotalPickedUp.ShouldBe(100);
        body.RemainingBottles["glass"].ShouldBe(0);
        body.RemainingBottles["metal"].ShouldBe(0);
        body.RemainingBottles["plastic"].ShouldBe(50);
    }

    private record PickupBottlesResponse
    {
        public Dictionary<string, int> BottlesPickedUp { get; init; } = new();
        public int TotalPickedUp { get; init; }
        public Dictionary<string, int> RemainingBottles { get; init; } = new();
    }
}