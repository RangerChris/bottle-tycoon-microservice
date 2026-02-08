using System.Net;
using System.Net.Http.Json;
using Shouldly;
using TruckService.Tests.TestFixtures;
using Xunit;

namespace TruckService.Tests.Integration;

public class TruckTelemetryEndpointTests : IClassFixture<TestcontainersFixture>
{
    private readonly TestcontainersFixture _fixture;

    public TruckTelemetryEndpointTests(TestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TruckTelemetry_UpdatesSuccessfully()
    {
        var client = _fixture.Client;

        var truckId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var createRequest = new { playerId, id = truckId, model = "Telemetry Test Truck", isActive = true };
        var createRes = await client.PostAsJsonAsync("/truck", createRequest, TestContext.Current.CancellationToken);
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);

        var telemetry = new { currentLoad = 50, capacity = 100, status = "loading" };
        var telemetryRes = await client.PostAsJsonAsync($"/trucks/{truckId}/telemetry", telemetry, TestContext.Current.CancellationToken);
        telemetryRes.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}