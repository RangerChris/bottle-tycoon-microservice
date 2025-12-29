using System.Net;
using System.Net.Http.Json;
using HeadquartersService.Endpoints;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace HeadquartersService.Tests;

public class HealthEndpointTests
{
    [Fact]
    public async Task HealthEndpoint_ShouldReturnHealthy()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); });

        var client = factory.CreateClient();
        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<HealthEndpoint.HealthResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Status.ShouldBe("Healthy");
        result.Checks.ShouldNotBeNull();
    }
}