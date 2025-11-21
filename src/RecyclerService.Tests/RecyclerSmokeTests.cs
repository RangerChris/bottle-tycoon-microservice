using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace RecyclerService.Tests;

public class RecyclerSmokeTests
{
    [Fact]
    public async Task GetRoot_ReturnsOkOrRedirect()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Testing"));
        var client = factory.CreateClient();
        var res = await client.GetAsync("/", TestContext.Current.CancellationToken);
        res.ShouldNotBeNull();
    }

    [Fact]
    public async Task Ping_ReturnsPong()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Testing"));
        var client = factory.CreateClient();
        var res = await client.GetAsync("/ping", TestContext.Current.CancellationToken);
        res.ShouldNotBeNull();
    }

    [Fact]
    public async Task HealthEndpoints_AreAccessible()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Testing"));
        var client = factory.CreateClient();

        var live = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);
        var ready = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        live.ShouldNotBeNull();
        ready.ShouldNotBeNull();
    }
}