using System.Net;
using GameService.Tests.TestFixtures;
using Shouldly;
using Xunit;

namespace GameService.Tests.Integration;

public class HealthEndpointTests(TestcontainersFixture fixture) : IClassFixture<TestcontainersFixture>
{
    [Fact]
    public async Task HealthEndpoint_ShouldReturnHealthyJson()
    {
        var client = fixture.Client;
        var res = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await res.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.ShouldContain("\"status\":\"Healthy\"");
    }
}