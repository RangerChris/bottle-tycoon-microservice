using System.Net;
using System.Net.Http.Json;
using HeadquartersService.Models;
using HeadquartersService.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace HeadquartersService.Tests;

public class GetDispatchDetailEndpointTests
{
    [Fact]
    public async Task InvalidGuid_ReturnsNotFound()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); });
        var client = factory.CreateClient();

        var resp = await client.GetAsync("/api/v1/headquarters/dispatch/not-a-guid", TestContext.Current.CancellationToken);
        resp.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MissingRequest_ReturnsNotFound()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); });
        var client = factory.CreateClient();

        var missingId = Guid.NewGuid();
        var resp = await client.GetAsync($"/api/v1/headquarters/dispatch/{missingId}", TestContext.Current.CancellationToken);
        resp.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExistingRequest_ReturnsOkAndBody()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            // no additional service configuration required
        });

        // Seed the dispatch queue in the test server's DI
        var scopeFactory = factory.Services.GetRequiredService<IServiceScopeFactory>();
        DispatchRequest seeded;
        using (var scope = scopeFactory.CreateScope())
        {
            var q = scope.ServiceProvider.GetRequiredService<IDispatchQueue>();
            seeded = new DispatchRequest { RecyclerId = Guid.NewGuid(), ExpectedBottles = 42, FullnessPercentage = 50, Priority = 1.23 };
            q.Enqueue(seeded);
        }

        var client = factory.CreateClient();
        var resp = await client.GetAsync($"/api/v1/headquarters/dispatch/{seeded.Id}", TestContext.Current.CancellationToken);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<DispatchRequest>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.Id.ShouldBe(seeded.Id);
        body.RecyclerId.ShouldBe(seeded.RecyclerId);
        body.ExpectedBottles.ShouldBe(seeded.ExpectedBottles);
        body.Priority.ShouldBe(seeded.Priority);
    }
}