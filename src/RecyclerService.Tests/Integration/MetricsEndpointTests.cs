using System.Net;
using System.Net.Http.Json;
using RecyclerService.Tests.TestFixtures;
using Shouldly;
using Xunit;

namespace RecyclerService.Tests.Integration;

public class MetricsEndpointTests : IClassFixture<TestcontainersFixture>
{
    private readonly TestcontainersFixture _fixture;

    public MetricsEndpointTests(TestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Metrics_ExposeBottlesProcessedByType()
    {
        var client = _fixture.Client;

        var recyclerId = Guid.NewGuid();
        var createRequest = new CreateRequest(recyclerId, "Metrics Test", 100, null);
        var createRes = await client.PostAsJsonAsync("/recyclers", createRequest, TestContext.Current.CancellationToken);
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);

        var visitor = new CustomerRequest
        {
            BottleCounts = new Dictionary<string, int> { { "glass", 3 }, { "metal", 2 }, { "plastic", 1 } },
            CustomerType = "Metrics"
        };

        var res = await client.PostAsJsonAsync($"/recyclers/{recyclerId}/customers", visitor, TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        await Task.Delay(200, TestContext.Current.CancellationToken);

        var metricsRes = await client.GetAsync("/metrics", TestContext.Current.CancellationToken);
        metricsRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await metricsRes.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.ShouldContain("bottles_processed_bottles_total");
        body.ShouldContain("bottle_type=\"glass\"");
        body.ShouldContain("bottle_type=\"metal\"");
        body.ShouldContain("bottle_type=\"plastic\"");
    }

    [Fact]
    public async Task Metrics_ExposeRecyclerCurrentBottles()
    {
        var client = _fixture.Client;

        var recyclerId = Guid.NewGuid();
        var createRequest = new CreateRequest(recyclerId, "Metrics Test", 100, null);
        var createRes = await client.PostAsJsonAsync("/recyclers", createRequest, TestContext.Current.CancellationToken);
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);

        var telemetry = new TelemetryRequest
        {
            BottleCounts = new Dictionary<string, int> { { "glass", 4 }, { "metal", 3 }, { "plastic", 2 } }
        };

        var telemetryRes = await client.PostAsJsonAsync($"/recyclers/{recyclerId}/telemetry", telemetry, TestContext.Current.CancellationToken);
        telemetryRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var telemetryBody = await telemetryRes.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        telemetryBody.ShouldContain(recyclerId.ToString());

        await Task.Delay(200, TestContext.Current.CancellationToken);

        var metricsRes = await client.GetAsync("/metrics", TestContext.Current.CancellationToken);
        metricsRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await metricsRes.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.ShouldContain("recycler_current_bottles");
    }

    private sealed record CreateRequest(Guid Id, string Name, int Capacity, string? Location);

    private sealed record CustomerRequest
    {
        public Dictionary<string, int>? BottleCounts { get; init; }
        public string? CustomerType { get; init; }
    }

    private sealed record TelemetryRequest
    {
        public Dictionary<string, int>? BottleCounts { get; init; }
    }
}