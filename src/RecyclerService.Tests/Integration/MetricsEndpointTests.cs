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

        var visitor = new VisitorRequest
        {
            BottleCounts = new Dictionary<string, int> { { "glass", 3 }, { "metal", 2 }, { "plastic", 1 } },
            VisitorType = "Metrics"
        };

        var res = await client.PostAsJsonAsync($"/recyclers/{recyclerId}/visitors", visitor, TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        var metricsRes = await client.GetAsync("/metrics", TestContext.Current.CancellationToken);
        metricsRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await metricsRes.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.ShouldContain("bottles_processed_bottles_total");
        body.ShouldContain("bottle_type=\"glass\"");
        body.ShouldContain("bottle_type=\"metal\"");
        body.ShouldContain("bottle_type=\"plastic\"");
    }

    private sealed record CreateRequest(Guid Id, string Name, int Capacity, string? Location);

    private sealed record VisitorRequest
    {
        public Dictionary<string, int>? BottleCounts { get; init; }
        public string? VisitorType { get; init; }
    }
}