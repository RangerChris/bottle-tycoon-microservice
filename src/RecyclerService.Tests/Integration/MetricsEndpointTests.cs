using System.Net;
using System.Net.Http.Json;
using RecyclerService.Tests.TestFixtures;
using Shouldly;
using Xunit;

namespace RecyclerService.Tests.Integration;

public class MetricsEndpointTests(TestcontainersFixture fixture) : IClassFixture<TestcontainersFixture>
{
    [Fact]
    public async Task Metrics_ExposeBottlesProcessedByType()
    {
        var client = fixture.Client;

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

        var body = await WaitForMetricsAsync(
            client,
            metrics => metrics.Contains("bottles_processed_bottles_total", StringComparison.OrdinalIgnoreCase) &&
                       metrics.Contains("bottle_type=\"glass\"", StringComparison.OrdinalIgnoreCase) &&
                       metrics.Contains("bottle_type=\"metal\"", StringComparison.OrdinalIgnoreCase) &&
                       metrics.Contains("bottle_type=\"plastic\"", StringComparison.OrdinalIgnoreCase),
            TestContext.Current.CancellationToken);

        body.ShouldContain("bottles_processed_bottles_total");
        body.ShouldContain("bottle_type=\"glass\"");
        body.ShouldContain("bottle_type=\"metal\"");
        body.ShouldContain("bottle_type=\"plastic\"");
    }

    [Fact]
    public async Task Metrics_ExposeRecyclerCurrentBottles()
    {
        var client = fixture.Client;

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

    [Fact]
    public async Task Metrics_ExcludeSoldRecyclerFromLiveGauges()
    {
        var client = fixture.Client;

        var recyclerId = Guid.NewGuid();
        var createRequest = new CreateRequest(recyclerId, "Metrics Sell Test", 100, null);
        var createRes = await client.PostAsJsonAsync("/recyclers", createRequest, TestContext.Current.CancellationToken);
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);

        var telemetry = new TelemetryRequest
        {
            BottleCounts = new Dictionary<string, int> { { "glass", 5 }, { "metal", 1 }, { "plastic", 2 } }
        };

        var telemetryRes = await client.PostAsJsonAsync($"/recyclers/{recyclerId}/telemetry", telemetry, TestContext.Current.CancellationToken);
        telemetryRes.StatusCode.ShouldBe(HttpStatusCode.OK);

        var syncRes = await client.GetAsync("/recyclers", TestContext.Current.CancellationToken);
        syncRes.StatusCode.ShouldBe(HttpStatusCode.OK);

        var beforeSellMetrics = await WaitForMetricsAsync(
            client,
            body => body.Contains("recycler_current_bottles", StringComparison.OrdinalIgnoreCase) &&
                    body.Contains($"recycler_id=\"{recyclerId}\"", StringComparison.OrdinalIgnoreCase),
            TestContext.Current.CancellationToken);
        beforeSellMetrics.ShouldContain("recycler_current_bottles");
        beforeSellMetrics.ShouldContain($"recycler_id=\"{recyclerId}\"");

        var sellRes = await client.PostAsJsonAsync($"/recyclers/{recyclerId}/sell", new { PlayerId = Guid.NewGuid() }, TestContext.Current.CancellationToken);
        sellRes.StatusCode.ShouldBe(HttpStatusCode.OK);

        var afterSellMetrics = await WaitForMetricsAsync(
            client,
            body => !body.Contains($"recycler_current_bottles{{recycler_id=\"{recyclerId}\"", StringComparison.OrdinalIgnoreCase) &&
                    body.Contains("recycler_active_state{", StringComparison.OrdinalIgnoreCase) &&
                    body.Contains($"recycler_id=\"{recyclerId}\"", StringComparison.OrdinalIgnoreCase) &&
                    body.Contains("} 0", StringComparison.OrdinalIgnoreCase),
            TestContext.Current.CancellationToken);
        afterSellMetrics.ShouldNotContain($"recycler_current_bottles{{recycler_id=\"{recyclerId}\"");
        afterSellMetrics.ShouldContain("recycler_active_state{");
        afterSellMetrics.ShouldContain($"recycler_id=\"{recyclerId}\"");
        afterSellMetrics.ShouldContain("} 0");
    }

    [Fact]
    public async Task Metrics_ShowRecyclersAfterServiceRestart()
    {
        var client = fixture.Client;

        var recyclerId = Guid.NewGuid();
        var createRequest = new CreateRequest(recyclerId, "Restart Test", 100, null);
        var createRes = await client.PostAsJsonAsync("/recyclers", createRequest, TestContext.Current.CancellationToken);
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);

        var fetchRes = await client.GetAsync("/recyclers", TestContext.Current.CancellationToken);
        fetchRes.StatusCode.ShouldBe(HttpStatusCode.OK);

        var metricsAfterFetch = await WaitForMetricsAsync(
            client,
            body => body.Contains("recycler_active_state", StringComparison.OrdinalIgnoreCase) &&
                    body.Contains($"recycler_id=\"{recyclerId}\"", StringComparison.OrdinalIgnoreCase),
            TestContext.Current.CancellationToken);

        metricsAfterFetch.ShouldContain("recycler_active_state");
        metricsAfterFetch.ShouldContain($"recycler_id=\"{recyclerId}\"");
        metricsAfterFetch.ShouldContain("recycler_active_state{");
    }

    private static async Task<string> WaitForMetricsAsync(
        HttpClient client,
        Func<string, bool> predicate,
        CancellationToken ct,
        int maxAttempts = 80,
        int delayMs = 250)
    {
        string lastBody = string.Empty;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            lastBody = await client.GetStringAsync("/metrics", ct);
            if (predicate(lastBody))
            {
                return lastBody;
            }

            await Task.Delay(delayMs, ct);
        }

        return lastBody;
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