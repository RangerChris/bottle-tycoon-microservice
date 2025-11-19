using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using TruckService.Models;
using Xunit;

namespace TruckService.Tests;

public class GetTruckEndpointTests
{
    [Fact]
    public async Task GetExistingTruck_ReturnsOk()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); });

        var client = factory.CreateClient();
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var res = await client.GetAsync($"/truck/{id}", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await res.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var truck = JsonSerializer.Deserialize<TruckDto>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        truck.ShouldNotBeNull();
        truck.Id.ShouldBe(id);
        truck.LicensePlate.ShouldBe("TRK-001");
    }

    [Fact]
    public async Task GetMissingTruck_ReturnsNotFound()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); });

        var client = factory.CreateClient();
        var id = Guid.NewGuid();
        var res = await client.GetAsync($"/truck/{id}", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}