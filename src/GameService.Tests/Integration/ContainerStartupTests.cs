using GameService.Tests.TestFixtures;
using Xunit;

namespace GameService.Tests.Integration;

public class ContainerStartupTests : IClassFixture<TestcontainersFixture>
{
    private readonly TestcontainersFixture _fixture;

    public ContainerStartupTests(TestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void ShouldStartContainerWithoutErrors()
    {
        Assert.True(_fixture.Started, "Container should have started successfully");
    }
}