using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace TruckService.Tests;

public class TruckServiceSmokeTests
{
    [Fact]
    public async Task GetRoot_ReturnsOk()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); });

        var client = factory.CreateClient();
        var res = await client.GetAsync("/", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await res.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Root may redirect to Swagger UI in Testing/Development; accept either plain text or Swagger UI HTML
        if (content.StartsWith("<"))
        {
            content.ShouldContain("Swagger UI");
        }
        else
        {
            content.ShouldBe("TruckService OK");
        }
    }
}