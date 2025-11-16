using System.Net;
using System.Net.Http.Json;
using GameService.Data;
using GameService.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace GameService.Tests;

public class CreateGetPlayerEndpointTests
{
    [Fact]
    public async Task CreatePlayer_ThenGetPlayer_ShouldReturnPlayer()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<GameDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                var connection = new SqliteConnection("DataSource=:memory:");
                connection.Open();
                services.AddDbContext<GameDbContext>(options => options.UseSqlite(connection));
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
                db.Database.EnsureCreated();
            });
        });

        var client = factory.CreateClient();

        var createRes = await client.PostAsync("/players", null, TestContext.Current.CancellationToken);
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<Player>(cancellationToken: TestContext.Current.CancellationToken);
        created.ShouldNotBeNull();

        var getRes = await client.GetAsync($"/players/{created.Id}", TestContext.Current.CancellationToken);
        getRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var got = await getRes.Content.ReadFromJsonAsync<Player>(cancellationToken: TestContext.Current.CancellationToken);
        got.ShouldNotBeNull();
        got.Id.ShouldBe(created.Id);
    }

    [Fact]
    public async Task CreditCredits_ShouldIncreasePlayerBalance()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<GameDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                var connection = new SqliteConnection("DataSource=:memory:");
                connection.Open();
                services.AddDbContext<GameDbContext>(options => options.UseSqlite(connection));
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
                db.Database.EnsureCreated();
            });
        });

        var client = factory.CreateClient();

        // Create player
        var createRes = await client.PostAsync("/players", null, TestContext.Current.CancellationToken);
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<Player>(cancellationToken: TestContext.Current.CancellationToken);
        created.ShouldNotBeNull();

        // Credit credits
        var creditReq = new { Amount = 100m, Reason = "test credit" };
        var creditRes = await client.PostAsJsonAsync($"/players/{created!.Id}/credit", creditReq, TestContext.Current.CancellationToken);
        creditRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var creditResult = await creditRes.Content.ReadFromJsonAsync<bool>(cancellationToken: TestContext.Current.CancellationToken);
        creditResult.ShouldBeTrue();

        // Get player and check balance
        var getRes = await client.GetAsync($"/players/{created.Id}", TestContext.Current.CancellationToken);
        getRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var player = await getRes.Content.ReadFromJsonAsync<Player>(cancellationToken: TestContext.Current.CancellationToken);
        player!.Credits.ShouldBe(1100m); // Starting 1000 + 100
    }

    [Fact]
    public async Task DebitCredits_ShouldDecreasePlayerBalance()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<GameDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                var connection = new SqliteConnection("DataSource=:memory:");
                connection.Open();
                services.AddDbContext<GameDbContext>(options => options.UseSqlite(connection));
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
                db.Database.EnsureCreated();
            });
        });

        var client = factory.CreateClient();

        // Create player
        var createRes = await client.PostAsync("/players", null, TestContext.Current.CancellationToken);
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<Player>(cancellationToken: TestContext.Current.CancellationToken);
        created.ShouldNotBeNull();

        // Debit credits
        var debitReq = new { Amount = 50m, Reason = "test debit" };
        var debitRes = await client.PostAsJsonAsync($"/players/{created!.Id}/debit", debitReq, TestContext.Current.CancellationToken);
        debitRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var debitResult = await debitRes.Content.ReadFromJsonAsync<bool>(cancellationToken: TestContext.Current.CancellationToken);
        debitResult.ShouldBeTrue();

        // Get player and check balance
        var getRes = await client.GetAsync($"/players/{created.Id}", TestContext.Current.CancellationToken);
        getRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var player = await getRes.Content.ReadFromJsonAsync<Player>(cancellationToken: TestContext.Current.CancellationToken);
        player!.Credits.ShouldBe(950m); // Starting 1000 - 50
    }

    [Fact]
    public async Task DebitCredits_InsufficientFunds_ShouldReturnError()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<GameDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                var connection = new SqliteConnection("DataSource=:memory:");
                connection.Open();
                services.AddDbContext<GameDbContext>(options => options.UseSqlite(connection));
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
                db.Database.EnsureCreated();
            });
        });

        var client = factory.CreateClient();

        // Create player
        var createRes = await client.PostAsync("/players", null, TestContext.Current.CancellationToken);
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<Player>(cancellationToken: TestContext.Current.CancellationToken);
        created.ShouldNotBeNull();

        // Try to debit more than available
        var debitReq = new { Amount = 2000m, Reason = "test debit" };
        var debitRes = await client.PostAsJsonAsync($"/players/{created!.Id}/debit", debitReq, TestContext.Current.CancellationToken);
        debitRes.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errorMsg = await ReadFirstErrorMessage(debitRes);
        errorMsg.ShouldContain("Insufficient credits");
    }

    [Fact]
    public async Task GetPlayer_NonExistentPlayer_ReturnsNotFound()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<GameDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                var connection = new SqliteConnection("DataSource=:memory:");
                connection.Open();
                services.AddDbContext<GameDbContext>(options => options.UseSqlite(connection));
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
                db.Database.EnsureCreated();
            });
        });

        var client = factory.CreateClient();

        var response = await client.GetAsync($"/players/{Guid.NewGuid()}", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreditCredits_InvalidAmount_ReturnsError()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<GameDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                var connection = new SqliteConnection("DataSource=:memory:");
                connection.Open();
                services.AddDbContext<GameDbContext>(options => options.UseSqlite(connection));
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
                db.Database.EnsureCreated();
            });
        });

        var client = factory.CreateClient();

        // Create player
        var createRes = await client.PostAsync("/players", null, TestContext.Current.CancellationToken);
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<Player>(cancellationToken: TestContext.Current.CancellationToken);
        created.ShouldNotBeNull();

        // Try to credit negative amount
        var creditReq = new { Amount = -100m, Reason = "invalid" };
        var creditRes = await client.PostAsJsonAsync($"/players/{created!.Id}/credit", creditReq, TestContext.Current.CancellationToken);
        creditRes.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errorMsg = await ReadFirstErrorMessage(creditRes);
        errorMsg.ShouldContain("Amount must be positive");
    }

    [Fact]
    public async Task DebitCredits_InvalidAmount_ReturnsError()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<GameDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                var connection = new SqliteConnection("DataSource=:memory:");
                connection.Open();
                services.AddDbContext<GameDbContext>(options => options.UseSqlite(connection));
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
                db.Database.EnsureCreated();
            });
        });

        var client = factory.CreateClient();

        // Create player
        var createRes = await client.PostAsync("/players", null, TestContext.Current.CancellationToken);
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<Player>(cancellationToken: TestContext.Current.CancellationToken);
        created.ShouldNotBeNull();

        // Try to debit negative amount
        var debitReq = new { Amount = -50m, Reason = "invalid" };
        var debitRes = await client.PostAsJsonAsync($"/players/{created!.Id}/debit", debitReq, TestContext.Current.CancellationToken);
        debitRes.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errorMsg = await ReadFirstErrorMessage(debitRes);
        errorMsg.ShouldContain("Amount must be positive");
    }

    [Fact]
    public async Task CreditCredits_NonExistentPlayer_ReturnsError()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<GameDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                var connection = new SqliteConnection("DataSource=:memory:");
                connection.Open();
                services.AddDbContext<GameDbContext>(options => options.UseSqlite(connection));
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
                db.Database.EnsureCreated();
            });
        });

        var client = factory.CreateClient();

        var creditReq = new { Amount = 100m, Reason = "test" };
        var creditRes = await client.PostAsJsonAsync($"/players/{Guid.NewGuid()}/credit", creditReq, TestContext.Current.CancellationToken);
        creditRes.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errorMsg = await ReadFirstErrorMessage(creditRes);
        errorMsg.ShouldContain("Player not found");
    }

    [Fact]
    public async Task DebitCredits_NonExistentPlayer_ReturnsError()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<GameDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                var connection = new SqliteConnection("DataSource=:memory:");
                connection.Open();
                services.AddDbContext<GameDbContext>(options => options.UseSqlite(connection));
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
                db.Database.EnsureCreated();
            });
        });

        var client = factory.CreateClient();

        var debitReq = new { Amount = 50m, Reason = "test" };
        var debitRes = await client.PostAsJsonAsync($"/players/{Guid.NewGuid()}/debit", debitReq, TestContext.Current.CancellationToken);
        debitRes.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errorMsg = await ReadFirstErrorMessage(debitRes);
        errorMsg.ShouldContain("Insufficient credits or player not found");
    }

    private static async Task<string> ReadFirstErrorMessage(HttpResponseMessage res)
    {
        var content = await res.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        if (string.IsNullOrWhiteSpace(content)) return string.Empty;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            var root = doc.RootElement;

            // Common FastEndpoints shape: { errors: { "": ["msg"] } }
            if (root.TryGetProperty("errors", out var errors))
            {
                // If errors is an object and contains empty-string property
                if (errors.ValueKind == System.Text.Json.JsonValueKind.Object
                    && errors.TryGetProperty("", out var def)
                    && def.ValueKind == System.Text.Json.JsonValueKind.Array
                    && def.GetArrayLength() > 0)
                    return def[0].GetString() ?? content;

                // If errors is an array or object with other keys, try to extract first string value
                foreach (var prop in errors.EnumerateObject())
                {
                    var v = prop.Value;
                    if (v.ValueKind == System.Text.Json.JsonValueKind.Array && v.GetArrayLength() > 0)
                        return v[0].GetString() ?? content;
                    if (v.ValueKind == System.Text.Json.JsonValueKind.String)
                        return v.GetString() ?? content;
                }
            }

            // ProblemDetails shape: { title: "..", detail: ".." }
            if (root.TryGetProperty("detail", out var detail)) return detail.GetString() ?? content;
            if (root.TryGetProperty("title", out var title)) return title.GetString() ?? content;

            // Fallback: if root is an array or string
            if (root.ValueKind == System.Text.Json.JsonValueKind.Array && root.GetArrayLength() > 0 && root[0].ValueKind == System.Text.Json.JsonValueKind.String)
                return root[0].GetString() ?? content;
        }
        catch (System.Text.Json.JsonException)
        {
            // Not JSON, fall through
        }

        return content;
    }
}