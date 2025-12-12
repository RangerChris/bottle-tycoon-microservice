using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace TruckService.Tests;

public class TruckSmokeTests
{
    [Fact]
    public async Task Root_ReturnsOkOrRedirect_InTesting()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Testing"));
        var client = factory.CreateClient();
        var res = await client.GetAsync("/", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Root_ReturnsOkOrRedirect_InDevelopment()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Development"));
        var client = factory.CreateClient();
        var res = await client.GetAsync("/", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
    }
}