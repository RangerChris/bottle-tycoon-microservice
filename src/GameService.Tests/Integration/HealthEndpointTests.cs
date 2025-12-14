using System.Net;
using System.Net.Http.Json;
using GameService.Tests.TestFixtures;
using Shouldly;
using Xunit;

namespace GameService.Tests.Integration;

public class HealthEndpointTests : IClassFixture<TestcontainersFixture>
{
    private readonly TestcontainersFixture _fixture;

    public HealthEndpointTests(TestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task HealthEndpoint_ShouldReturnHealthyJson()
    {
        var client = _fixture.Client;
        var res = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await res.Content.ReadFromJsonAsync<dynamic>(TestContext.Current.CancellationToken);
        ((string)content!.status).ShouldBe("Healthy");
    }
}