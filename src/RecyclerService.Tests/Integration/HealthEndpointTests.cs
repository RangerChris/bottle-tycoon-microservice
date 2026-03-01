using System.Net;
using System.Net.Http.Json;
using RecyclerService.Tests.TestFixtures;
using Shouldly;
using Xunit;

namespace RecyclerService.Tests.Integration;

public class HealthEndpointTests(TestcontainersFixture fixture) : IClassFixture<TestcontainersFixture>
{
    [Fact]
    public async Task HealthEndpoint_ShouldReturnHealthyJson()
    {
        var client = fixture.Client;
        var res = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<HealthResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.Status.ShouldBe("Healthy");
        // In Testing environment, no specific checks are configured
    }


    public sealed record HealthResponse(string Status, Dictionary<string, string> Checks);
}