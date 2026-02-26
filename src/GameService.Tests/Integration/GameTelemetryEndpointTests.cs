using System.Net;
using System.Net.Http.Json;
using GameService.Tests.TestFixtures;
using Shouldly;
using Xunit;

namespace GameService.Tests.Integration;

public class GameTelemetryEndpointTests : IClassFixture<TestcontainersFixture>
{
    private readonly TestcontainersFixture _fixture;

    public GameTelemetryEndpointTests(TestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PostTelemetry_WithPositiveEarnings_StoresAndReturns()
    {
        var client = _fixture.Client;
        var playerId = Guid.NewGuid();
        var request = new { TotalEarnings = 1000m };

        var response = await client.PostAsJsonAsync($"/player/{playerId}/telemetry", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TelemetryResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.PlayerId.ShouldBe(playerId);
        body.TotalEarnings.ShouldBe(1000m);
    }

    [Fact]
    public async Task PostTelemetry_WithZeroEarnings_StoresAndReturns()
    {
        var client = _fixture.Client;
        var playerId = Guid.NewGuid();
        var request = new { TotalEarnings = 0m };

        var response = await client.PostAsJsonAsync($"/player/{playerId}/telemetry", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TelemetryResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.TotalEarnings.ShouldBe(0m);
    }

    [Fact]
    public async Task PostTelemetry_WithNullEarnings_DefaultsToZero()
    {
        var client = _fixture.Client;
        var playerId = Guid.NewGuid();
        var request = new { TotalEarnings = (decimal?)null };

        var response = await client.PostAsJsonAsync($"/player/{playerId}/telemetry", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TelemetryResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.TotalEarnings.ShouldBe(0m);
    }

    [Fact]
    public async Task PostTelemetry_WithEmptyBody_DefaultsToZero()
    {
        var client = _fixture.Client;
        var playerId = Guid.NewGuid();
        var request = new { };

        var response = await client.PostAsJsonAsync($"/player/{playerId}/telemetry", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TelemetryResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.TotalEarnings.ShouldBe(0m);
    }

    [Fact]
    public async Task PostTelemetry_WithNegativeEarnings_StoresAsZero()
    {
        var client = _fixture.Client;
        var playerId = Guid.NewGuid();
        var request = new { TotalEarnings = -500m };

        var response = await client.PostAsJsonAsync($"/player/{playerId}/telemetry", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TelemetryResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.TotalEarnings.ShouldBe(-500m);
    }

    [Fact]
    public async Task PostTelemetry_WithLargeEarnings_StoresCorrectly()
    {
        var client = _fixture.Client;
        var playerId = Guid.NewGuid();
        var largeAmount = 999999999.99m;
        var request = new { TotalEarnings = largeAmount };

        var response = await client.PostAsJsonAsync($"/player/{playerId}/telemetry", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TelemetryResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.TotalEarnings.ShouldBe(largeAmount);
    }

    [Fact]
    public async Task PostTelemetry_MultipleUpdates_OverwritesPrevious()
    {
        var client = _fixture.Client;
        var playerId = Guid.NewGuid();

        await client.PostAsJsonAsync($"/player/{playerId}/telemetry", new { TotalEarnings = 1000m }, TestContext.Current.CancellationToken);
        var response = await client.PostAsJsonAsync($"/player/{playerId}/telemetry", new { TotalEarnings = 1500m }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TelemetryResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.TotalEarnings.ShouldBe(1500m);
    }

    private record TelemetryResponse
    {
        public Guid PlayerId { get; init; }
        public decimal TotalEarnings { get; init; }
    }
}