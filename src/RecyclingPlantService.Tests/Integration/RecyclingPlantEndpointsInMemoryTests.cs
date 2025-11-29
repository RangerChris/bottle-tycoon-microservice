using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecyclingPlantService.Data;
using Shouldly;
using Xunit;

namespace RecyclingPlantService.Tests.Integration;

public class RecyclingPlantEndpointsInMemoryTests
{
    private WebApplicationFactory<Program> CreateFactory(string? dbName = null)
    {
        var uniqueDb = dbName ?? Guid.NewGuid().ToString();

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, conf) =>
            {
                var cfg = new ConfigurationBuilder()
                    .AddInMemoryCollection(new[]
                    {
                        new KeyValuePair<string, string?>("ENABLE_MESSAGING", "false")
                    })
                    .Build();

                conf.AddConfiguration(cfg);
            });

            // Ensure each factory uses its own InMemory database instance to avoid cross-test pollution
            builder.ConfigureServices(services =>
            {
                // remove existing DbContext registration if present
                var existing = services.Where(d => d.ServiceType == typeof(DbContextOptions<RecyclingPlantDbContext>)).ToList();
                foreach (var d in existing)
                {
                    services.Remove(d);
                }

                services.AddDbContext<RecyclingPlantDbContext>(options => options.UseInMemoryDatabase(uniqueDb));
            });
        });
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
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

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
            Assert.True(false, "Missing 'status' property in response (any casing)");
        }

        if (TryGetPropertyCaseInsensitive(body, "timestamp", out var tsEl))
        {
            var ts = tsEl.ValueKind == JsonValueKind.String ? tsEl.GetString() : tsEl.ToString();
            ts.ShouldNotBeNull();
            // ensure it's a valid DateTimeOffset
            DateTimeOffset.Parse(ts!);
        }
        else
        {
            Assert.True(false, "Missing 'timestamp' property in response (any casing)");
        }
    }

    [Fact]
    public async Task GetDeliveries_ReturnsSeededDeliveries_OrderedDescending()
    {
        await using var factory = CreateFactory();

        // Seed data into isolated in-memory DB
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecyclingPlantDbContext>();
            db.PlantDeliveries.AddRange(new[]
            {
                new PlantDelivery { Id = Guid.NewGuid(), TruckId = Guid.NewGuid(), PlayerId = Guid.NewGuid(), GlassCount = 1, MetalCount = 0, PlasticCount = 0, GrossEarnings = 4m, OperatingCost = 1m, NetEarnings = 3m, DeliveredAt = DateTimeOffset.UtcNow.AddMinutes(-1) },
                new PlantDelivery { Id = Guid.NewGuid(), TruckId = Guid.NewGuid(), PlayerId = Guid.NewGuid(), GlassCount = 2, MetalCount = 1, PlasticCount = 0, GrossEarnings = 10.5m, OperatingCost = 2m, NetEarnings = 8.5m, DeliveredAt = DateTimeOffset.UtcNow },
                new PlantDelivery { Id = Guid.NewGuid(), TruckId = Guid.NewGuid(), PlayerId = Guid.NewGuid(), GlassCount = 0, MetalCount = 3, PlasticCount = 2, GrossEarnings = 11.5m, OperatingCost = 1m, NetEarnings = 10.5m, DeliveredAt = DateTimeOffset.UtcNow.AddMinutes(-2) }
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = factory.CreateClient();
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
        await using var factory = CreateFactory();

        var playerId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecyclingPlantDbContext>();
            db.PlayerEarnings.Add(new PlayerEarnings { PlayerId = playerId, TotalEarnings = 123.45m, DeliveryCount = 3, AverageEarnings = 41.15m });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = factory.CreateClient();
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
    public async Task GetPlayerEarningsBreakdown_CalculatesCorrectly()
    {
        await using var factory = CreateFactory();
        var playerId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecyclingPlantDbContext>();
            db.PlantDeliveries.AddRange(new[]
            {
                new PlantDelivery { Id = Guid.NewGuid(), TruckId = Guid.NewGuid(), PlayerId = playerId, GlassCount = 2, MetalCount = 1, PlasticCount = 0, GrossEarnings = 0m, OperatingCost = 0m, NetEarnings = 0m, DeliveredAt = DateTimeOffset.UtcNow },
                new PlantDelivery { Id = Guid.NewGuid(), TruckId = Guid.NewGuid(), PlayerId = playerId, GlassCount = 0, MetalCount = 2, PlasticCount = 3, GrossEarnings = 0m, OperatingCost = 0m, NetEarnings = 0m, DeliveredAt = DateTimeOffset.UtcNow }
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = factory.CreateClient();
        var res = await client.GetAsync($"/api/v1/recycling-plant/players/{playerId}/earnings/breakdown", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        var breakdown = await res.Content.ReadFromJsonAsync<EarningsBreakdown>(TestContext.Current.CancellationToken);
        breakdown.ShouldNotBeNull();
        breakdown.GlassCount.ShouldBe(2);
        breakdown.MetalCount.ShouldBe(3);
        breakdown.PlasticCount.ShouldBe(3);
        breakdown.GlassEarnings.ShouldBe(2 * 4.0m);
        breakdown.MetalEarnings.ShouldBe(3 * 2.5m);
        breakdown.PlasticEarnings.ShouldBe(3 * 1.75m);
    }

    [Fact]
    public async Task GetTopEarners_ReturnsTopNOrdered()
    {
        await using var factory = CreateFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecyclingPlantDbContext>();
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var c = Guid.NewGuid();
            db.PlayerEarnings.AddRange(new[]
            {
                new PlayerEarnings { PlayerId = a, TotalEarnings = 100m },
                new PlayerEarnings { PlayerId = b, TotalEarnings = 300m },
                new PlayerEarnings { PlayerId = c, TotalEarnings = 200m }
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = factory.CreateClient();
        var res = await client.GetAsync($"/api/v1/recycling-plant/reports/top-earners?Count=2", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        var list = await res.Content.ReadFromJsonAsync<List<PlayerEarnings>>(TestContext.Current.CancellationToken);
        list.ShouldNotBeNull();
        list.Count.ShouldBe(2);
        list[0].TotalEarnings.ShouldBeGreaterThan(list[1].TotalEarnings);
    }
}