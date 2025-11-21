using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace GameService.Tests;

public class GameStartupTests
{
    [Fact]
    public async Task Start_InTestingEnvironment_DoesNotThrow()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Testing"));
        var client = factory.CreateClient();
        var res = await client.GetAsync("/", TestContext.Current.CancellationToken);
        res.ShouldNotBeNull();
    }

    [Fact]
    public async Task Start_InDevelopmentEnvironment_DoesNotThrow()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Development"));
        var client = factory.CreateClient();
        var res = await client.GetAsync("/", TestContext.Current.CancellationToken);
        res.ShouldNotBeNull();
    }
}