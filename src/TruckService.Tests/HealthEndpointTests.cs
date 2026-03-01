using System.Net;
using System.Net.Http.Json;
using Shouldly;
using TruckService.Endpoints;
using TruckService.Tests.TestFixtures;
using Xunit;

namespace TruckService.Tests;

public class HealthEndpointTests(TestcontainersFixture fixture) : IClassFixture<TestcontainersFixture>
{
    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        var client = fixture.Client;
        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<HealthEndpoint.HealthResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Status.ShouldBe("Healthy");
    }
}