using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using TruckService.Endpoints.CreateTruck;
using TruckService.Models;
using Xunit;

namespace TruckService.Tests;

public class CrudTruckEndpointTests
{
    [Fact]
    public async Task CreateListUpdateDeleteFlow_Works()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); });

        var client = factory.CreateClient();

        // Create
        var createReq = new CreateTruckRequest { LicensePlate = "NEW-123", Model = "Model-X", IsActive = true };
        var createRes = await client.PostAsJsonAsync("/trucks", createReq, TestContext.Current.CancellationToken);
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = JsonSerializer.Deserialize<TruckDto>(await createRes.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        created.ShouldNotBeNull();

        var id = created.Id;

        // Get
        var getRes = await client.GetAsync($"/truck/{id}", TestContext.Current.CancellationToken);
        getRes.StatusCode.ShouldBe(HttpStatusCode.OK);

        // List
        var listRes = await client.GetAsync("/trucks", TestContext.Current.CancellationToken);
        listRes.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Update
        var updateReq = new TruckDto { Id = id, LicensePlate = "NEW-321", Model = "Model-Y", IsActive = false };
        var putRes = await client.PutAsJsonAsync($"/truck/{id}", updateReq, TestContext.Current.CancellationToken);
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
}