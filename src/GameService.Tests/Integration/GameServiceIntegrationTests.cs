using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GameService.Tests.TestFixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Shouldly;
using Xunit;

namespace GameService.Tests.Integration;

public class GameServiceIntegrationTests : IAsyncLifetime
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

    private async Task SeedPlayersAsync(string connectionString, Guid aliceId, Guid bobId)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS ""players"" (
                ""Id"" uuid PRIMARY KEY,
                ""Credits"" numeric(18,2) NOT NULL,
                ""CreatedAt"" timestamptz NOT NULL,
                ""UpdatedAt"" timestamptz NOT NULL
            );

            INSERT INTO ""players"" (""Id"", ""Credits"", ""CreatedAt"", ""UpdatedAt"") VALUES (@aId, 1000, now(), now()) ON CONFLICT (""Id"") DO NOTHING;
            INSERT INTO ""players"" (""Id"", ""Credits"", ""CreatedAt"", ""UpdatedAt"") VALUES (@bId, 1200, now(), now()) ON CONFLICT (""Id"") DO NOTHING;
        ";
        cmd.Parameters.AddWithValue("aId", aliceId);
        cmd.Parameters.AddWithValue("bId", bobId);
        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task PostPlayer_CreatesPlayer_AndGetPlayer_ReturnsSeededAndCreated()
    {
        var aliceId = Guid.NewGuid();
        var bobId = Guid.NewGuid();

        // If the container didn't start, prefer to fail fast since this test seeds Postgres directly
        if (!_containers.Started)
        {
            throw new InvalidOperationException("Postgres testcontainer did not start; cannot run Postgres-seeding integration test.");
        }

        await SeedPlayersAsync(_containers.Postgres.ConnectionString, aliceId, bobId);

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, conf) =>
            {
                var cfg = new ConfigurationBuilder()
                    .AddInMemoryCollection(new[]
                    {
                        new KeyValuePair<string, string?>("ConnectionStrings:GameStateConnection", _containers.Postgres.ConnectionString),
                        new KeyValuePair<string, string?>("APPLY_MIGRATIONS", "true"),
                        new KeyValuePair<string, string?>("ENABLE_MESSAGING", "false")
                    })
                    .Build();

                conf.AddConfiguration(cfg);
            });
        });

        var client = factory.CreateClient();

        // Create a new player via API
        var createRes = await client.PostAsync("/player", null, TestContext.Current.CancellationToken);
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        created.ValueKind.ShouldBe(JsonValueKind.Object);
        created.TryGetProperty("Id", out _).ShouldBeTrue();

        // Get seeded player
        var getRes = await client.GetAsync($"/player/{aliceId}", TestContext.Current.CancellationToken);
        getRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var got = await getRes.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        got.ValueKind.ShouldBe(JsonValueKind.Object);
        got.GetProperty("Id").GetGuid().ShouldBe(aliceId);

        // Ensure seeded higher credits player exists
        var getResBob = await client.GetAsync($"/player/{bobId}", TestContext.Current.CancellationToken);
        getResBob.StatusCode.ShouldBe(HttpStatusCode.OK);
        var gotBob = await getResBob.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        gotBob.GetProperty("Credits").GetDecimal().ShouldBe(1200);
    }
}