using System.Net;
using Shouldly;
using TruckService.Tests.TestFixtures;
using Xunit;

namespace TruckService.Tests;

public class TruckServiceSmokeTests : IClassFixture<TestcontainersFixture>
{
    private readonly TestcontainersFixture _fixture;

    public TruckServiceSmokeTests(TestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetRoot_ReturnsOk()
    {
        var client = _fixture.Client;
        var res = await client.GetAsync("/", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await res.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Root may redirect to Swagger UI in Testing/Development; accept either plain text or Swagger UI HTML
        if (content.StartsWith("<"))
        {
            content.ShouldContain("Swagger UI");
        }
        else
        {
            content.ShouldBe("TruckService OK");
        }
    }
}