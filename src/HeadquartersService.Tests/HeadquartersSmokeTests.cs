using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace HeadquartersService.Tests;

public class HeadquartersSmokeTests
{
    [Fact]
    public async Task ReadinessEndpoint_ReturnsOk()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); });
        var client = factory.CreateClient();
        var res = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
        res.IsSuccessStatusCode.ShouldBeTrue();
    }

    [Fact]
    public async Task ReadinessEndpoint_StatusCodeIsOk()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); });
        var client = factory.CreateClient();
        var res = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
    }
}