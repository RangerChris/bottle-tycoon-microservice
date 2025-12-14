using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TruckService.Data;
using TruckService.Endpoints.CreateTruck;
using TruckService.Models;
using TruckService.Services;
using TruckService.Tests.TestFixtures;
using Xunit;

namespace TruckService.Tests;

public class CrudTruckEndpointTests : IClassFixture<TestcontainersFixture>
{
    private readonly TestcontainersFixture _fixture;

    public CrudTruckEndpointTests(TestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateListUpdateDeleteFlow_Works()
    {
        var client = _fixture.Client;

        // Create
        var createReq = new CreateTruckRequest { LicensePlate = "NEW-123", Model = "Model-X", IsActive = true };
        var createRes = await client.PostAsJsonAsync("/truck", createReq, TestContext.Current.CancellationToken);
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = JsonSerializer.Deserialize<TruckDto>(await createRes.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        created.ShouldNotBeNull();

        var id = created.Id;

        // Get
        var getRes = await client.GetAsync($"/truck/{id}", TestContext.Current.CancellationToken);
        getRes.StatusCode.ShouldBe(HttpStatusCode.OK);

        // List
        var listRes = await client.GetAsync("/truck", TestContext.Current.CancellationToken);
        listRes.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Update (body-only)
        var updateReq = new { truckId = id, licensePlate = "NEW-321", model = "Model-Y", isActive = false };
        var putRes = await client.PutAsJsonAsync("/truck", updateReq, TestContext.Current.CancellationToken);
        putRes.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Patch status
        var patchReq = new { IsActive = true };
        var patchRes = await client.PatchAsJsonAsync($"/truck/{id}/status", patchReq, TestContext.Current.CancellationToken);
        patchRes.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Delete
        var delRes = await client.DeleteAsync($"/truck/{id}", TestContext.Current.CancellationToken);
        delRes.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Confirm deleted
        var getAfter = await client.GetAsync($"/truck/{id}", TestContext.Current.CancellationToken);
        getAfter.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ProcessNextDelivery_AdvancesDeliveryState()
    {
        // Clear database to ensure test isolation
        using (var scope = _fixture.Host!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TruckDbContext>();
            db.Deliveries.RemoveRange(db.Deliveries);
            db.Trucks.RemoveRange(db.Trucks);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Seed a truck and a queued delivery
        Guid truckId, deliveryId;
        using (var scope = _fixture.Host!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TruckDbContext>();
            var truck = new TruckEntity { Id = Guid.NewGuid(), LicensePlate = "PROC-001", Model = "Test", IsActive = true };
            db.Trucks.Add(truck);
            var delivery = new DeliveryEntity
            {
                Id = Guid.NewGuid(),
                TruckId = truck.Id,
                RecyclerId = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                State = "Queued",
                LoadByTypeJson = @"{""glass"":1,""metal"":2,""plastic"":0}",
                GrossEarnings = 9m,
                OperatingCost = 1.5m,
                NetProfit = 5m
            };
            db.Deliveries.Add(delivery);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            truckId = truck.Id;
            deliveryId = delivery.Id;
        }

        // Call RunOnceAsync directly
        using (var scope = _fixture.Host!.Services.CreateScope())
        {
            var worker = scope.ServiceProvider.GetRequiredService<IRouteWorker>();
            await worker.RunOnceAsync(TestContext.Current.CancellationToken);
        }

        // Verify delivery completed and truck earnings updated
        using (var scope = _fixture.Host!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TruckDbContext>();
            var delivery = await db.Deliveries.FindAsync([deliveryId], Xunit.TestContext.Current.CancellationToken);
            delivery.ShouldNotBeNull();
            delivery.State.ShouldBe("Completed");
            delivery.CompletedAt.ShouldNotBeNull();

            var truck = await db.Trucks.FindAsync([truckId], Xunit.TestContext.Current.CancellationToken);
            truck.ShouldNotBeNull();
            truck.TotalEarnings.ShouldBe(5m);
        }
    }

    [Fact]
    public async Task DispatchTruck_Succeeds()
    {
        var client = _fixture.Client;

        // Create a truck
        var createReq = new CreateTruckRequest { LicensePlate = "DISP-123", Model = "Model-D", IsActive = true };
        var createRes = await client.PostAsJsonAsync("/truck", createReq, TestContext.Current.CancellationToken);
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = JsonSerializer.Deserialize<TruckDto>(await createRes.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        created.ShouldNotBeNull();
        var truckId = created.Id;

        // Dispatch the truck
        var dispatchReq = new { TruckId = truckId, RecyclerId = Guid.NewGuid(), DistanceKm = 10.0 };
        var dispatchRes = await client.PostAsJsonAsync($"/api/v1/truck/{truckId}/dispatch", dispatchReq, TestContext.Current.CancellationToken);
        dispatchRes.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await dispatchRes.Content.ReadFromJsonAsync<bool>(TestContext.Current.CancellationToken);
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task GetTruckEarnings_ReturnsEarnings()
    {
        var client = _fixture.Client;

        // Create a truck
        var createReq = new CreateTruckRequest { LicensePlate = "EARN-123", Model = "Model-E", IsActive = true };
        var createRes = await client.PostAsJsonAsync("/truck", createReq, TestContext.Current.CancellationToken);
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = JsonSerializer.Deserialize<TruckDto>(await createRes.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        created.ShouldNotBeNull();
        var truckId = created.Id;

        // Get earnings
        var earningsRes = await client.GetAsync($"/api/v1/truck/{truckId}/earnings", TestContext.Current.CancellationToken);
        earningsRes.StatusCode.ShouldBe(HttpStatusCode.OK);

        var earnings = await earningsRes.Content.ReadFromJsonAsync<decimal>(TestContext.Current.CancellationToken);
        earnings.ShouldBe(0m); // new truck
    }

    [Fact]
    public async Task GetFleetSummary_ReturnsFleet()
    {
        var client = _fixture.Client;

        // Create a truck
        var createReq = new CreateTruckRequest { LicensePlate = "FLEET-123", Model = "Model-F", IsActive = true };
        var createRes = await client.PostAsJsonAsync("/truck", createReq, TestContext.Current.CancellationToken);
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Get fleet summary
        var fleetRes = await client.GetAsync("/api/v1/truck/fleet/summary", TestContext.Current.CancellationToken);
        fleetRes.StatusCode.ShouldBe(HttpStatusCode.OK);

        var fleet = await fleetRes.Content.ReadFromJsonAsync<IEnumerable<TruckStatusDto>>(TestContext.Current.CancellationToken);
        fleet.ShouldNotBeNull();
        fleet.Count().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GetTruckStatus_ReturnsStatus()
    {
        var client = _fixture.Client;

        // Create a truck
        var createReq = new CreateTruckRequest { LicensePlate = "STAT-123", Model = "Model-S", IsActive = true };
        var createRes = await client.PostAsJsonAsync("/truck", createReq, TestContext.Current.CancellationToken);
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = JsonSerializer.Deserialize<TruckDto>(await createRes.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        created.ShouldNotBeNull();
        var truckId = created.Id;

        // Get status
        var statusRes = await client.GetAsync($"/api/v1/truck/{truckId}/status", TestContext.Current.CancellationToken);
        statusRes.StatusCode.ShouldBe(HttpStatusCode.OK);

        var status = await statusRes.Content.ReadFromJsonAsync<TruckStatusDto>(TestContext.Current.CancellationToken);
        status.ShouldNotBeNull();
        status.Id.ShouldBe(truckId);
    }
}