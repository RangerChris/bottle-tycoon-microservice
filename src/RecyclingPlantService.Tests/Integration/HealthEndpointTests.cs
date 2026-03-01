using System.Net;
using System.Net.Http.Json;
using RecyclingPlantService.Endpoints;
using RecyclingPlantService.Tests.TestFixtures;
using Shouldly;
using Xunit;

namespace RecyclingPlantService.Tests.Integration;

public class HealthEndpointTests(TestcontainersFixture fixture) : IClassFixture<TestcontainersFixture>
{
    [Fact]
    public async Task HealthEndpoint_ShouldReturnHealthy()
    {
        var client = fixture.Client;
        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<HealthEndpoint.HealthResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Status.ShouldBe("Healthy");
        result.Checks.ShouldNotBeNull();
    }

    [Fact]
    public async Task HealthEndpoint_ShouldReturnJsonContentType()
    {
        var client = fixture.Client;
        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
    }
}