using System.Net;
using Shouldly;
using TruckService.Tests.TestFixtures;
using Xunit;

namespace TruckService.Tests;

public class TruckSmokeTests : IClassFixture<TestcontainersFixture>
{
    private readonly TestcontainersFixture _fixture;

    public TruckSmokeTests(TestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Root_ReturnsOkOrRedirect_InTesting()
    {
        var client = _fixture.Client;
        var res = await client.GetAsync("/", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Root_ReturnsOkOrRedirect_InDevelopment()
    {
        var client = _fixture.Client;
        var res = await client.GetAsync("/", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
    }
}