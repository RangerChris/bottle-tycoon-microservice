using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace HeadquartersService.Tests;

public class HeadquartersSmokeTests
{
    [Fact]
    public async Task GetRoot_ReturnsOk()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); });
        var client = factory.CreateClient();
        var res = await client.GetAsync("/ping", TestContext.Current.CancellationToken);
        res.EnsureSuccessStatusCode();
        var text = await res.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        text.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PingEndpoint_ReturnsPongExactly()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); });
        var client = factory.CreateClient();
        var res = await client.GetAsync("/ping", TestContext.Current.CancellationToken);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldBe("pong");
    }
}