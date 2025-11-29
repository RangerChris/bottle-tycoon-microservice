using System.Net;
using System.Net.Http.Json;
using HeadquartersService.Models;
using HeadquartersService.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Testing.Platform.Services;
using Shouldly;
using Xunit;
using IServiceScopeFactory = Microsoft.Extensions.DependencyInjection.IServiceScopeFactory;

namespace HeadquartersService.Tests;

public class FleetAndQueueEndpointTests
{
    [Fact]
    public async Task FleetStatus_ReturnsSeededTrucks()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                /* no-op, we'll seed below via Build callback */
            });
        });

        // Retrieve the IFleetService from the test server to seed it
        var scopeFactory = factory.Services.GetRequiredService<IServiceScopeFactory>();
        using (var scope = scopeFactory.CreateScope())
        {
            var fleet = scope.ServiceProvider.GetRequiredService<IFleetService>();
            fleet.AddTruck(new Truck { Capacity = 100, Reliability = 0.95 });
            fleet.AddTruck(new Truck { Capacity = 50, Reliability = 0.9 });
        }

        var client = factory.CreateClient();
        var resp = await client.GetAsync("/api/v1/headquarters/fleet/status", TestContext.Current.CancellationToken);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var trucks = await resp.Content.ReadFromJsonAsync<List<Truck>>(TestContext.Current.CancellationToken);
        trucks.ShouldNotBeNull();
        trucks.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task DispatchQueue_ReturnsSeededRequests()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                /* no-op */
            });
        });

        var scopeFactory = factory.Services.GetRequiredService<IServiceScopeFactory>();
        using (var scope = scopeFactory.CreateScope())
        {
            var q = scope.ServiceProvider.GetRequiredService<IDispatchQueue>();
            q.Enqueue(new DispatchRequest { RecyclerId = Guid.NewGuid(), ExpectedBottles = 100, Priority = 1.0 });
            q.Enqueue(new DispatchRequest { RecyclerId = Guid.NewGuid(), ExpectedBottles = 50, Priority = 0.5 });
        }

        var client = factory.CreateClient();
        var resp = await client.GetAsync("/api/v1/headquarters/dispatch-queue", TestContext.Current.CancellationToken);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var list = await resp.Content.ReadFromJsonAsync<List<DispatchRequest>>(TestContext.Current.CancellationToken);
        list.ShouldNotBeNull();
        list.Count.ShouldBeGreaterThanOrEqualTo(2);
    }
}