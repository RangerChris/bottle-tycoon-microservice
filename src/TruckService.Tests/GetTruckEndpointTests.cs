using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TruckService.Data;
using TruckService.Models;
using Xunit;

namespace TruckService.Tests;

public class GetTruckEndpointTests
{
    [Fact]
    public async Task GetExistingTruck_ReturnsOk()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); });

        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");

        // seed the expected truck into the test host's database
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TruckDbContext>();
            if (await db.Trucks.FindAsync([id], Xunit.TestContext.Current.CancellationToken) is null)
            {
                db.Trucks.Add(new TruckEntity { Id = id, LicensePlate = "TRK-001", Model = "M", IsActive = true });
                await db.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);
            }
        }

        var client = factory.CreateClient();
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