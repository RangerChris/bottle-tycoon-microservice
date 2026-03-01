using GameService.Tests.TestFixtures;
using Xunit;

namespace GameService.Tests.Integration;

public class ContainerStartupTests(TestcontainersFixture fixture) : IClassFixture<TestcontainersFixture>
{
    [Fact]
    public void ShouldStartContainerWithoutErrors()
    {
        Assert.True(fixture.Started, "Container should have started successfully");
    }
}