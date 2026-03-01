using Shouldly;
using TruckService.Tests.TestFixtures;
using Xunit;

namespace TruckService.Tests;

public class ProgramPathsTests(TestcontainersFixture fixture) : IClassFixture<TestcontainersFixture>
{
    [Fact]
    public async Task Start_Works()
    {
        var client = fixture.Client;
        var res = await client.GetAsync("/", TestContext.Current.CancellationToken);
        res.ShouldNotBeNull();
    }
}