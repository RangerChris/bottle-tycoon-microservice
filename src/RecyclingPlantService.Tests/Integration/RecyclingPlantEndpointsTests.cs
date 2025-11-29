using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using RecyclingPlantService.Tests.TestFixtures;
using Shouldly;
using Xunit;

namespace RecyclingPlantService.Tests.Integration;

public class RecyclingPlantEndpointsTests : IAsyncLifetime
{
    private readonly TestcontainersFixture _containers = new();

    public ValueTask InitializeAsync()
    {
        return _containers.InitializeAsync();
    }

    public ValueTask DisposeAsync()
    {
        return _containers.DisposeAsync();
    }

    private async Task SeedDatabaseAsync(string connectionString, Guid aliceId, Guid bobId)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS ""Players"" (
                ""Id"" uuid PRIMARY KEY,
                ""DisplayName"" text
            );

            CREATE TABLE IF NOT EXISTS ""Deliveries"" (
                ""Id"" uuid PRIMARY KEY,
                ""PlayerId"" uuid REFERENCES ""Players""(""Id""),
                ""Amount"" numeric,
                ""Material"" text,
                ""CreatedAt"" timestamp
            );

            INSERT INTO ""Players"" (""Id"", ""DisplayName"") VALUES (@aliceId, 'Alice') ON CONFLICT (""Id"") DO NOTHING;
            INSERT INTO ""Players"" (""Id"", ""DisplayName"") VALUES (@bobId, 'Bob') ON CONFLICT (""Id"") DO NOTHING;

            INSERT INTO ""Deliveries"" (""Id"", ""PlayerId"", ""Amount"", ""Material"", ""CreatedAt"") VALUES
                (@d1, @aliceId, 100, 'Glass', now()),
                (@d2, @aliceId, 50, 'Plastic', now()),
                (@d3, @bobId, 120, 'Metal', now())
            ON CONFLICT (""Id"") DO NOTHING;
        ";
        cmd.Parameters.AddWithValue("aliceId", aliceId);
        cmd.Parameters.AddWithValue("bobId", bobId);
        cmd.Parameters.AddWithValue("d1", Guid.NewGuid());
        cmd.Parameters.AddWithValue("d2", Guid.NewGuid());
        cmd.Parameters.AddWithValue("d3", Guid.NewGuid());

        await cmd.ExecuteNonQueryAsync();
    }

    private static Guid? FindPlayerId(JsonElement el, Guid expectedA, Guid expectedB)
    {
        if (el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty("Id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
            {
                if (Guid.TryParse(idProp.GetString(), out var g))
                {
                    if (g == expectedA || g == expectedB)
                    {
                        return g;
                    }
                }
            }

            foreach (var prop in el.EnumerateObject())
            {
                var v = prop.Value;
                if (v.ValueKind == JsonValueKind.String)
                {
                    if (Guid.TryParse(v.GetString(), out var g))
                    {
                        if (g == expectedA || g == expectedB)
                        {
                            return g;
                        }
                    }
                }
            }
        }

        return null;
    }

    private static decimal? FindNumericValue(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in el.EnumerateObject())
            {
                var v = prop.Value;
                if (v.ValueKind == JsonValueKind.Number)
                {
                    if (v.TryGetDecimal(out var d))
                    {
                        return d;
                    }
                }

                if (v.ValueKind == JsonValueKind.String)
                {
                    if (decimal.TryParse(v.GetString(), out var d))
                    {
                        return d;
                    }
                }
            }
        }

        return null;
    }

    [Fact]
    public async Task StatusEndpoint_ReturnsOperational()
    {
        if (!_containers.IsAvailable)
        {
            return;
        }

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((context, conf) =>
            {
                var cfg = new ConfigurationBuilder()
                    .AddInMemoryCollection(new[]
                    {
                        new KeyValuePair<string, string?>("ConnectionStrings:RecyclingPlantConnection", _containers.Postgres.ConnectionString),
                        new KeyValuePair<string, string?>("Redis", "localhost:6379"),
                        new KeyValuePair<string, string?>("ENABLE_MESSAGING", "false")
                    })
                    .Build();

                conf.AddConfiguration(cfg);
            });
        });

        var client = factory.CreateClient();
        var res = await client.GetAsync("/api/v1/recycling-plant/status", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("Status").GetString().ShouldBe("Operational");
    }

    [Fact]
    public async Task TopEarnersEndpoint_ReturnsOrderedList_ByTotalEarnings()
    {
        if (!_containers.IsAvailable)
        {
            return;
        }

        var aliceId = Guid.NewGuid();
        var bobId = Guid.NewGuid();
        await SeedDatabaseAsync(_containers.Postgres.ConnectionString, aliceId, bobId);

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((context, conf) =>
            {
                var cfg = new ConfigurationBuilder()
                    .AddInMemoryCollection(new[]
                    {
                        new KeyValuePair<string, string?>("ConnectionStrings:RecyclingPlantConnection", _containers.Postgres.ConnectionString),
                        new KeyValuePair<string, string?>("ENABLE_MESSAGING", "false")
                    })
                    .Build();

                conf.AddConfiguration(cfg);
            });
        });

        var client = factory.CreateClient();
        var res = await client.GetAsync("/api/v1/recycling-plant/reports/top-earners?Count=5", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        var list = await res.Content.ReadFromJsonAsync<IEnumerable<JsonElement>>(TestContext.Current.CancellationToken);
        list.ShouldNotBeNull();

        var elements = list!.ToList();
        elements.Count.ShouldBeGreaterThanOrEqualTo(2);

        var aliceElement = elements.FirstOrDefault(e => FindPlayerId(e, aliceId, bobId) == aliceId);
        var bobElement = elements.FirstOrDefault(e => FindPlayerId(e, aliceId, bobId) == bobId);

        aliceElement.ValueKind.ShouldNotBe(JsonValueKind.Undefined);
        bobElement.ValueKind.ShouldNotBe(JsonValueKind.Undefined);

        var aliceTotal = FindNumericValue(aliceElement).ShouldNotBeNull().ToString(CultureInfo.InvariantCulture);
        var bobTotal = FindNumericValue(bobElement).ShouldNotBeNull().ToString(CultureInfo.InvariantCulture);

        decimal.Parse(aliceTotal).ShouldBeGreaterThan(decimal.Parse(bobTotal));

        var firstPlayerId = FindPlayerId(elements[0], aliceId, bobId);
        firstPlayerId.ShouldBe(aliceId);
    }

    [Fact]
    public async Task DeliveriesEndpoint_ReturnsPagedResults()
    {
        if (!_containers.IsAvailable)
        {
            return;
        }

        var aliceId = Guid.NewGuid();
        var bobId = Guid.NewGuid();
        await SeedDatabaseAsync(_containers.Postgres.ConnectionString, aliceId, bobId);
        await using var conn = new NpgsqlConnection(_containers.Postgres.ConnectionString);
        await conn.OpenAsync(TestContext.Current.CancellationToken);
        var extraCmd = conn.CreateCommand();
        extraCmd.CommandText = @"
            INSERT INTO ""Deliveries"" (""Id"", ""PlayerId"", ""Amount"", ""Material"", ""CreatedAt"") VALUES
                (@d4, @aliceId, 30, 'Paper', now())
            ON CONFLICT (""Id"") DO NOTHING;
        ";
        extraCmd.Parameters.AddWithValue("d4", Guid.NewGuid());
        extraCmd.Parameters.AddWithValue("aliceId", aliceId);
        await extraCmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((context, conf) =>
            {
                var cfg = new ConfigurationBuilder()
                    .AddInMemoryCollection([
                        new KeyValuePair<string, string?>("ConnectionStrings:RecyclingPlantConnection", _containers.Postgres.ConnectionString),
                        new KeyValuePair<string, string?>("ENABLE_MESSAGING", "false")
                    ])
                    .Build();

                conf.AddConfiguration(cfg);
            });
        });

        var client = factory.CreateClient();
        var res = await client.GetAsync("/api/v1/recycling-plant/deliveries?page=1&size=2", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        var list = await res.Content.ReadFromJsonAsync<IEnumerable<JsonElement>>(TestContext.Current.CancellationToken);
        list.ShouldNotBeNull();

        var items = list!.ToList();
        items.Count.ShouldBeLessThanOrEqualTo(2);
        items.Count.ShouldBeGreaterThan(0);

        foreach (var item in items)
        {
            item.TryGetProperty("Id", out _).ShouldBeTrue();
            item.TryGetProperty("PlayerId", out _).ShouldBeTrue();
            FindNumericValue(item).ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task DeliveriesEndpoint_DefaultPaging_ShouldReturnResults()
    {
        if (!_containers.IsAvailable)
        {
            return;
        }

        var aliceId = Guid.NewGuid();
        var bobId = Guid.NewGuid();
        await SeedDatabaseAsync(_containers.Postgres.ConnectionString, aliceId, bobId);

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((context, conf) =>
            {
                var cfg = new ConfigurationBuilder()
                    .AddInMemoryCollection([
                        new KeyValuePair<string, string?>("ConnectionStrings:RecyclingPlantConnection", _containers.Postgres.ConnectionString),
                        new KeyValuePair<string, string?>("ENABLE_MESSAGING", "false")
                    ])
                    .Build();

                conf.AddConfiguration(cfg);
            });
        });

        var client = factory.CreateClient();
        var res = await client.GetAsync("/api/v1/recycling-plant/deliveries", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        var list = await res.Content.ReadFromJsonAsync<IEnumerable<JsonElement>>(TestContext.Current.CancellationToken);
        list.ShouldNotBeNull();
        list!.Any().ShouldBeTrue();
    }

    [Fact]
    public async Task PlayerEarningsEndpoints_ReturnsDataAndCorrectTotals()
    {
        if (!_containers.IsAvailable)
        {
            return;
        }

        var aliceId = Guid.NewGuid();
        var bobId = Guid.NewGuid();
        await SeedDatabaseAsync(_containers.Postgres.ConnectionString, aliceId, bobId);

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((context, conf) =>
            {
                var cfg = new ConfigurationBuilder()
                    .AddInMemoryCollection([
                        new KeyValuePair<string, string?>("ConnectionStrings:RecyclingPlantConnection", _containers.Postgres.ConnectionString),
                        new KeyValuePair<string, string?>("ENABLE_MESSAGING", "false")
                    ])
                    .Build();

                conf.AddConfiguration(cfg);
            });
        });

        var client = factory.CreateClient();

        var earningsRes = await client.GetAsync($"/api/v1/recycling-plant/players/{aliceId}/earnings", TestContext.Current.CancellationToken);
        earningsRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var earningsBody = await earningsRes.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var total = FindNumericValue(earningsBody).ShouldNotBeNull().ToString();
        decimal.Parse(total).ShouldBe(150);

        var breakdownRes = await client.GetAsync($"/api/v1/recycling-plant/players/{aliceId}/earnings/breakdown", TestContext.Current.CancellationToken);
        breakdownRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var breakdown = await breakdownRes.Content.ReadFromJsonAsync<IEnumerable<JsonElement>>(TestContext.Current.CancellationToken);
        breakdown.ShouldNotBeNull();
        var breakdownList = breakdown!.ToList();
        breakdownList.Count.ShouldBeGreaterThanOrEqualTo(2);

        var historyRes = await client.GetAsync($"/api/v1/recycling-plant/players/{aliceId}/earnings/history", TestContext.Current.CancellationToken);
        historyRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var histList = await historyRes.Content.ReadFromJsonAsync<IEnumerable<JsonElement>>(TestContext.Current.CancellationToken);
        histList.ShouldNotBeNull();
        histList!.Count().ShouldBeGreaterThanOrEqualTo(2);
    }
}