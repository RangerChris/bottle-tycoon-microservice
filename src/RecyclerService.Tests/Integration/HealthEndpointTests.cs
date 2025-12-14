﻿using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RecyclerService.Tests.TestFixtures;
using Shouldly;
using Xunit;

namespace RecyclerService.Tests.Integration;

public class HealthEndpointTests : IClassFixture<TestcontainersFixture>
{
    private readonly TestcontainersFixture _fixture;

    public HealthEndpointTests(TestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task HealthEndpoint_ShouldReturnHealthyJson()
    {
        var client = _fixture.Client;
        var res = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<HealthResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.Status.ShouldBe("Healthy");
        // In Testing environment, no specific checks are configured
    }


    public sealed record HealthResponse(string Status, Dictionary<string, string> Checks);
}