using System.Net;
using System.Net.Http.Json;
using Shouldly;
using RecyclerService.Tests.TestFixtures;
using Xunit;

namespace RecyclerService.Tests.Integration;

public class RecyclerTelemetryEndpointSimpleTests : IClassFixture<TestcontainersFixture>
{
    private readonly TestcontainersFixture _fixture;

    public RecyclerTelemetryEndpointSimpleTests(TestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TelemetryEndpoint_UpdatesSuccessfully()
    {
        var client = _fixture.Client;
        var recyclerId = Guid.NewGuid();

        var createRequest = new { id = recyclerId, name = "Test", capacity = 100, location = (string?)null };
        var createRes = await client.PostAsJsonAsync("/recyclers", createRequest, TestContext.Current.CancellationToken);
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);

        var telemetry = new { bottleCounts = new Dictionary<string, int> { { "glass", 10 } } };
        var telemetryRes = await client.PostAsJsonAsync($"/recyclers/{recyclerId}/telemetry", telemetry, TestContext.Current.CancellationToken);

        telemetryRes.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}