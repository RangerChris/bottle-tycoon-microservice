using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using TruckService.Endpoints.CreateTruck;
using TruckService.Models;
using TruckService.Tests.TestFixtures;
using Xunit;

namespace TruckService.Tests;

public class UpgradeTruckEndpointTests : IClassFixture<TestcontainersFixture>
{
    private readonly TestcontainersFixture _fixture;

    public UpgradeTruckEndpointTests(TestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UpgradeTruck_IncreasesLevel_AndUpdatesModel()
    {
        var client = _fixture.Client;

        // 1. Create a truck
        var createReq = new CreateTruckRequest { Model = "Standard Truck", IsActive = true };
        var createRes = await client.PostAsJsonAsync("/truck", createReq, TestContext.Current.CancellationToken);
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = JsonSerializer.Deserialize<TruckDto>(
            await createRes.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        created.ShouldNotBeNull();
        created.Level.ShouldBe(0);

        var truckId = created.Id;

        // 2. Upgrade to Level 1
        var upgRes1 = await client.PostAsJsonAsync($"/truck/{truckId}/upgrade", new { }, TestContext.Current.CancellationToken);
        upgRes1.StatusCode.ShouldBe(HttpStatusCode.OK);

        var upgDto1 = JsonSerializer.Deserialize<TruckDto>(
            await upgRes1.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        upgDto1.ShouldNotBeNull();
        upgDto1.Level.ShouldBe(1);
        upgDto1.Model.ShouldBe("Standard Truck Mk 2");

        // 3. Upgrade to Level 2
        var upgRes2 = await client.PostAsJsonAsync($"/truck/{truckId}/upgrade", new { }, TestContext.Current.CancellationToken);
        upgRes2.StatusCode.ShouldBe(HttpStatusCode.OK);
        var upgDto2 = JsonSerializer.Deserialize<TruckDto>(
            await upgRes2.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        upgDto2?.Level.ShouldBe(2);
        upgDto2?.Model.ShouldBe("Standard Truck Mk 3");

        // 4. Upgrade to Level 3 (Max)
        var upgRes3 = await client.PostAsJsonAsync($"/truck/{truckId}/upgrade", new { }, TestContext.Current.CancellationToken);
        upgRes3.StatusCode.ShouldBe(HttpStatusCode.OK);
        var upgDto3 = JsonSerializer.Deserialize<TruckDto>(
            await upgRes3.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        upgDto3?.Level.ShouldBe(3);
        upgDto3?.Model.ShouldBe("Standard Truck Mk 4");

        // 5. Attempt Upgrade beyond Max
        var upgRes4 = await client.PostAsJsonAsync($"/truck/{truckId}/upgrade", new { }, TestContext.Current.CancellationToken);
        upgRes4.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpgradeTruck_ReturnsNotFound_ForInvalidId()
    {
        var client = _fixture.Client;
        var invalidId = Guid.NewGuid();

        var res = await client.PostAsJsonAsync($"/truck/{invalidId}/upgrade", new { }, TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}