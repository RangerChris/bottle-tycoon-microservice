using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RecyclingPlantService.Data;
using RecyclingPlantService.Services;
using RecyclingPlantService.Tests.TestFixtures;
using Shouldly;
using Xunit;

namespace RecyclingPlantService.Tests.Integration;

public class RecyclingPlantEndpointsTests : IClassFixture<TestcontainersFixture>
{
    private readonly TestcontainersFixture _fixture;

    public RecyclingPlantEndpointsTests(TestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private static bool TryGetPropertyCaseInsensitive(JsonElement el, string name, out JsonElement value)
    {
        value = default;
        if (el.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        // try exact
        if (el.TryGetProperty(name, out var direct))
        {
            value = direct;
            return true;
        }

        foreach (var p in el.EnumerateObject())
        {
            if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = p.Value;
                return true;
            }
        }

        return false;
    }

    [Fact]
    public async Task StatusEndpoint_ReturnsOperational()
    {
        var client = _fixture.Client;

        var res = await client.GetAsync("/api/v1/recycling-plant/status", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.ValueKind.ShouldBe(JsonValueKind.Object);

        if (TryGetPropertyCaseInsensitive(body, "status", out var statusEl))
        {
            var s = statusEl.ValueKind == JsonValueKind.String ? statusEl.GetString() : statusEl.ToString();
            s.ShouldBe("Operational");
        }
        else
        {
            Assert.Fail("Missing 'status' property in response (any casing)");
        }

        if (TryGetPropertyCaseInsensitive(body, "timestamp", out var tsEl))
        {
            var ts = tsEl.ValueKind == JsonValueKind.String ? tsEl.GetString() : tsEl.ToString();
            ts.ShouldNotBeNull();
            // ensure it's a valid DateTimeOffset
            DateTimeOffset.Parse(ts);
        }
        else
        {
            Assert.Fail("Missing 'timestamp' property in response (any casing)");
        }
    }

    [Fact]
    public async Task GetDeliveries_ReturnsSeededDeliveries_OrderedDescending()
    {
        // Clear existing data to ensure isolation
        using (var scope = _fixture.Host!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecyclingPlantDbContext>();
            db.PlantDeliveries.RemoveRange(db.PlantDeliveries);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Seed data into isolated DB
        using (var scope = _fixture.Host!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecyclingPlantDbContext>();
            db.PlantDeliveries.AddRange(new PlantDelivery { Id = Guid.NewGuid(), TruckId = Guid.NewGuid(), PlayerId = Guid.NewGuid(), GlassCount = 1, MetalCount = 0, PlasticCount = 0, GrossEarnings = 4m, OperatingCost = 1m, NetEarnings = 3m, DeliveredAt = DateTimeOffset.UtcNow.AddMinutes(-1) },
                new PlantDelivery { Id = Guid.NewGuid(), TruckId = Guid.NewGuid(), PlayerId = Guid.NewGuid(), GlassCount = 2, MetalCount = 1, PlasticCount = 0, GrossEarnings = 10.5m, OperatingCost = 2m, NetEarnings = 8.5m, DeliveredAt = DateTimeOffset.UtcNow },
                new PlantDelivery { Id = Guid.NewGuid(), TruckId = Guid.NewGuid(), PlayerId = Guid.NewGuid(), GlassCount = 0, MetalCount = 3, PlasticCount = 2, GrossEarnings = 11.5m, OperatingCost = 1m, NetEarnings = 10.5m, DeliveredAt = DateTimeOffset.UtcNow.AddMinutes(-2) });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = _fixture.Client;
        var res = await client.GetAsync("/api/v1/recycling-plant/deliveries?page=1&pageSize=10", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        var jsonElements = await res.Content.ReadFromJsonAsync<IEnumerable<JsonElement>>(TestContext.Current.CancellationToken);
        jsonElements.ShouldNotBeNull();
        var items = jsonElements.ToList();
        items.Count.ShouldBe(3);

        // Ensure ordered by DeliveredAt desc
        DateTimeOffset? prev = null;
        foreach (var item in items)
        {
            if (TryGetPropertyCaseInsensitive(item, "deliveredAt", out var dEl))
            {
                var dStr = dEl.ValueKind == JsonValueKind.String ? dEl.GetString() : dEl.ToString();
                var cur = DateTimeOffset.Parse(dStr!);
                if (prev != null)
                {
                    cur.ShouldBeLessThanOrEqualTo(prev.Value);
                }

                prev = cur;
            }
            else if (item.TryGetProperty("DeliveredAt", out var dAlt) && dAlt.ValueKind == JsonValueKind.String)
            {
                var cur = DateTimeOffset.Parse(dAlt.GetString()!);
                if (prev != null)
                {
                    cur.ShouldBeLessThanOrEqualTo(prev.Value);
                }

                prev = cur;
            }
        }
    }

    [Fact]
    public async Task GetPlayerEarnings_ForExistingAndMissingPlayer_ReturnsExpected()
    {
        var playerId = Guid.NewGuid();

        // Clear existing data to ensure isolation
        using (var scope = _fixture.Host!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecyclingPlantDbContext>();
            db.PlayerEarnings.RemoveRange(db.PlayerEarnings);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var scope = _fixture.Host!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecyclingPlantDbContext>();
            db.PlayerEarnings.Add(new PlayerEarnings { PlayerId = playerId, TotalEarnings = 123.45m, DeliveryCount = 3, AverageEarnings = 41.15m });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = _fixture.Client;
        var res = await client.GetAsync($"/api/v1/recycling-plant/players/{playerId}/earnings", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        // locate the numeric total in the returned object
        var totalProp = body.EnumerateObject().FirstOrDefault(p => p.Value.ValueKind == JsonValueKind.Number);
        totalProp.Value.GetDecimal().ShouldBe(123.45m);

        // Missing player
        var missingId = Guid.NewGuid();
        var res2 = await client.GetAsync($"/api/v1/recycling-plant/players/{missingId}/earnings", TestContext.Current.CancellationToken);
        res2.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body2 = await res2.Content.ReadFromJsonAsync<PlayerEarnings>(TestContext.Current.CancellationToken);
        body2.ShouldNotBeNull();
        body2.PlayerId.ShouldBe(missingId);
        body2.TotalEarnings.ShouldBe(0);
    }

    [Fact]
    public async Task GetTopEarners_ReturnsTopNOrdered()
    {
        // Clear existing data to ensure isolation
        using (var scope = _fixture.Host!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecyclingPlantDbContext>();
            db.PlayerEarnings.RemoveRange(db.PlayerEarnings);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var scope = _fixture.Host!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecyclingPlantDbContext>();
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var c = Guid.NewGuid();
            db.PlayerEarnings.AddRange(new PlayerEarnings { PlayerId = a, TotalEarnings = 100m }, new PlayerEarnings { PlayerId = b, TotalEarnings = 300m }, new PlayerEarnings { PlayerId = c, TotalEarnings = 200m });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = _fixture.Client;
        var res = await client.GetAsync("/api/v1/recycling-plant/reports/top-earners?Count=2", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        var list = await res.Content.ReadFromJsonAsync<List<PlayerEarnings>>(TestContext.Current.CancellationToken);
        list.ShouldNotBeNull();
        list.Count.ShouldBe(2);
        list[0].TotalEarnings.ShouldBeGreaterThan(list[1].TotalEarnings);
    }

    [Fact]
    public async Task InitializeEndpoint_ResetsData()
    {
        // Clear existing data
        using (var scope = _fixture.Host!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecyclingPlantDbContext>();
            db.PlantDeliveries.RemoveRange(db.PlantDeliveries);
            db.PlayerEarnings.RemoveRange(db.PlayerEarnings);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Seed some data
        using (var scope = _fixture.Host!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecyclingPlantDbContext>();
            db.PlantDeliveries.Add(new PlantDelivery { Id = Guid.NewGuid(), TruckId = Guid.NewGuid(), PlayerId = Guid.NewGuid(), GlassCount = 1, MetalCount = 0, PlasticCount = 0, GrossEarnings = 4m, OperatingCost = 1m, NetEarnings = 3m, DeliveredAt = DateTimeOffset.UtcNow });
            db.PlayerEarnings.Add(new PlayerEarnings { PlayerId = Guid.NewGuid(), TotalEarnings = 100m, DeliveryCount = 1, AverageEarnings = 100m });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Verify data exists
        using (var scope = _fixture.Host!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecyclingPlantDbContext>();
            db.PlantDeliveries.Count().ShouldBe(1);
            db.PlayerEarnings.Count().ShouldBe(1);
        }

        // Call initialize
        var client = _fixture.Client;
        var res = await client.PostAsync("/initialize", null, TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Verify data is reset
        using (var scope = _fixture.Host!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecyclingPlantDbContext>();
            db.PlantDeliveries.Count().ShouldBe(0);
            db.PlayerEarnings.Count().ShouldBe(0);
        }
    }

    [Fact]
    public async Task GetPlayerEarningsHistory_ReturnsPlayerDeliveries()
    {
        var playerId = Guid.NewGuid();

        // Clear existing data to ensure isolation
        using (var scope = _fixture.Host!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecyclingPlantDbContext>();
            db.PlantDeliveries.RemoveRange(db.PlantDeliveries);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Seed deliveries for the player
        using (var scope = _fixture.Host!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecyclingPlantDbContext>();
            db.PlantDeliveries.AddRange(
                new PlantDelivery { Id = Guid.NewGuid(), TruckId = Guid.NewGuid(), PlayerId = playerId, GlassCount = 1, MetalCount = 0, PlasticCount = 0, GrossEarnings = 4m, OperatingCost = 1m, NetEarnings = 3m, DeliveredAt = DateTimeOffset.UtcNow.AddMinutes(-1) },
                new PlantDelivery { Id = Guid.NewGuid(), TruckId = Guid.NewGuid(), PlayerId = playerId, GlassCount = 2, MetalCount = 1, PlasticCount = 0, GrossEarnings = 10.5m, OperatingCost = 2m, NetEarnings = 8.5m, DeliveredAt = DateTimeOffset.UtcNow },
                new PlantDelivery { Id = Guid.NewGuid(), TruckId = Guid.NewGuid(), PlayerId = Guid.NewGuid(), GlassCount = 0, MetalCount = 3, PlasticCount = 2, GrossEarnings = 11.5m, OperatingCost = 1m, NetEarnings = 10.5m, DeliveredAt = DateTimeOffset.UtcNow.AddMinutes(-2) } // different player
            );
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = _fixture.Client;
        var res = await client.GetAsync($"/api/v1/recycling-plant/players/{playerId}/earnings/history?page=1&pageSize=10", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        var deliveries = await res.Content.ReadFromJsonAsync<List<PlantDelivery>>(TestContext.Current.CancellationToken);
        deliveries.ShouldNotBeNull();
        deliveries.Count.ShouldBe(2);

        // Ensure ordered by DeliveredAt desc
        deliveries[0].DeliveredAt.ShouldBeGreaterThan(deliveries[1].DeliveredAt);
    }

    [Fact]
    public async Task ProcessDeliveryAsync_CreatesDeliveryAndUpdatesEarnings()
    {
        // Clear existing data to ensure isolation
        using (var scope = _fixture.Host!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecyclingPlantDbContext>();
            db.PlantDeliveries.RemoveRange(db.PlantDeliveries);
            db.PlayerEarnings.RemoveRange(db.PlayerEarnings);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var serviceScope = _fixture.Host!.Services.CreateScope();
        var service = serviceScope.ServiceProvider.GetRequiredService<IRecyclingPlantService>();
        var recyclingPlantDbContext = serviceScope.ServiceProvider.GetRequiredService<RecyclingPlantDbContext>();

        var truckId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var loadByType = new Dictionary<string, int> { ["glass"] = 1, ["metal"] = 2 };
        var operatingCost = 1.5m;
        var deliveredAt = DateTimeOffset.UtcNow;

        var deliveryId = await service.ProcessDeliveryAsync(truckId, playerId, loadByType, operatingCost, deliveredAt);

        deliveryId.ShouldNotBe(Guid.Empty);

        var delivery = await recyclingPlantDbContext.PlantDeliveries.FindAsync([deliveryId], TestContext.Current.CancellationToken);
        delivery.ShouldNotBeNull();
        delivery.TruckId.ShouldBe(truckId);
        delivery.PlayerId.ShouldBe(playerId);
        delivery.GlassCount.ShouldBe(1);
        delivery.MetalCount.ShouldBe(2);
        delivery.PlasticCount.ShouldBe(0);
        delivery.GrossEarnings.ShouldBe(4m + 2.5m * 2); // 4 + 5 = 9
        delivery.OperatingCost.ShouldBe(operatingCost);
        delivery.NetEarnings.ShouldBe(9m - operatingCost);

        var earnings = await recyclingPlantDbContext.PlayerEarnings.FindAsync([playerId], TestContext.Current.CancellationToken);
        earnings.ShouldNotBeNull();
        earnings.TotalEarnings.ShouldBe(9m - operatingCost);
        earnings.DeliveryCount.ShouldBe(1);
        earnings.AverageEarnings.ShouldBe(9m - operatingCost);
    }

    [Fact]
    public async Task GetPlayerEarningsBreakdownAsync_ReturnsBreakdown()
    {
        var playerId = Guid.NewGuid();

        // Clear existing data to ensure isolation
        using (var scope = _fixture.Host!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecyclingPlantDbContext>();
            db.PlantDeliveries.RemoveRange(db.PlantDeliveries);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Seed deliveries
        using (var scope = _fixture.Host!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecyclingPlantDbContext>();
            db.PlantDeliveries.AddRange(
                new PlantDelivery { Id = Guid.NewGuid(), TruckId = Guid.NewGuid(), PlayerId = playerId, GlassCount = 1, MetalCount = 0, PlasticCount = 0, GrossEarnings = 4m, OperatingCost = 1m, NetEarnings = 3m, DeliveredAt = DateTimeOffset.UtcNow },
                new PlantDelivery { Id = Guid.NewGuid(), TruckId = Guid.NewGuid(), PlayerId = playerId, GlassCount = 0, MetalCount = 2, PlasticCount = 1, GrossEarnings = 7.25m, OperatingCost = 1m, NetEarnings = 6.25m, DeliveredAt = DateTimeOffset.UtcNow }
            );
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var serviceScope = _fixture.Host!.Services.CreateScope();
        var service = serviceScope.ServiceProvider.GetRequiredService<IRecyclingPlantService>();

        var breakdown = await service.GetPlayerEarningsBreakdownAsync(playerId);

        breakdown.ShouldNotBeNull();
        breakdown.GlassEarnings.ShouldBe(4m);
        breakdown.MetalEarnings.ShouldBe(5m);
        breakdown.PlasticEarnings.ShouldBe(1.75m);
        breakdown.GlassCount.ShouldBe(1);
        breakdown.MetalCount.ShouldBe(2);
        breakdown.PlasticCount.ShouldBe(1);
    }
}