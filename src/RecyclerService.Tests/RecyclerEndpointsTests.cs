using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace RecyclerService.Tests;

public class RecyclerEndpointsTests
{
    [Fact]
    public async Task CreateAndGetRecycler_Works()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((ctx, conf) => { conf.AddInMemoryCollection(new Dictionary<string, string?> { { "USE_INMEMORY", "true" } }); });
        });
        var client = factory.CreateClient();

        // Program registers InMemory when USE_INMEMORY=true and ensures DB creation in Testing environment

        var createReq = new
        {
            Name = "Test Recycler",
            Capacity = 100,
            Location = "Testville"
        };

        var createRes = await client.PostAsJsonAsync("/recyclers", createReq, CancellationToken.None);
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);

        var location = createRes.Headers.Location?.ToString();
        location.ShouldNotBeNullOrEmpty();

        // Extract id from location
        var idStr = location!.Split('/').Last();
        Guid.TryParse(idStr, out var id).ShouldBeTrue();

        var getRes = await client.GetAsync($"/recyclers/{id}", CancellationToken.None);
        getRes.StatusCode.ShouldBe(HttpStatusCode.OK);

        var dto = await getRes.Content.ReadFromJsonAsync<JsonElement>(Xunit.TestContext.Current.CancellationToken);
        dto.GetProperty("name").GetString().ShouldBe("Test Recycler");
        dto.GetProperty("capacity").GetInt32().ShouldBe(100);
    }

    [Fact]
    public async Task VisitorArrived_UpdatesLoad_AndReturnsOk()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((ctx, conf) => { conf.AddInMemoryCollection(new Dictionary<string, string?> { { "USE_INMEMORY", "true" } }); });
        });
        var client = factory.CreateClient();

        // Program registers InMemory when USE_INMEMORY=true and ensures DB creation in Testing environment

        var createReq = new { Name = "For Visitors", Capacity = 10, Location = "Nowhere" };
        var createRes = await client.PostAsJsonAsync("/recyclers", createReq, CancellationToken.None);
        createRes.EnsureSuccessStatusCode();
        var location = createRes.Headers.Location!.ToString();
        var idStr = location.Split('/').Last();
        Guid.TryParse(idStr, out var id).ShouldBeTrue();

        var visitorReq = new { Bottles = 5, VisitorType = "Regular" };
        var visRes = await client.PostAsJsonAsync($"/recyclers/{id}/visitors", visitorReq, CancellationToken.None);
        visRes.StatusCode.ShouldBe(HttpStatusCode.OK);

        var resBody = await visRes.Content.ReadFromJsonAsync<JsonElement>(Xunit.TestContext.Current.CancellationToken);
        resBody.GetProperty("currentLoad").GetInt32().ShouldBe(5);
        resBody.GetProperty("capacity").GetInt32().ShouldBe(10);
    }

    [Fact]
    public async Task VisitorArrived_Returns404_WhenRecyclerNotFound()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((ctx, conf) => { conf.AddInMemoryCollection(new Dictionary<string, string?> { { "USE_INMEMORY", "true" } }); });
        });
        var client = factory.CreateClient();

        // Program registers InMemory when USE_INMEMORY=true and ensures DB creation in Testing environment

        var visitorReq = new { Bottles = 3, VisitorType = "Regular" };
        var visRes = await client.PostAsJsonAsync($"/recyclers/{Guid.NewGuid()}/visitors", visitorReq, CancellationToken.None);
        visRes.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsJson()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => { b.UseEnvironment("Testing"); });
        var client = factory.CreateClient();

        var res = await client.GetAsync("/health", CancellationToken.None);
        res.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);

        var body = await res.Content.ReadAsStringAsync(CancellationToken.None);
        body.ShouldNotBeNullOrWhiteSpace();
    }
}