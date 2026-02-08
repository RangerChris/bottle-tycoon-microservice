using Shouldly;
using TruckService.Tests.TestFixtures;
using Xunit;

namespace TruckService.Tests;

public class ProgramPathsTests : IClassFixture<TestcontainersFixture>
{
    private readonly TestcontainersFixture _fixture;

    public ProgramPathsTests(TestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Start_Works()
    {
        var client = _fixture.Client;
        var res = await client.GetAsync("/", TestContext.Current.CancellationToken);
        res.ShouldNotBeNull();
    }
}