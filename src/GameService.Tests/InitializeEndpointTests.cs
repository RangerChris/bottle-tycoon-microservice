﻿using System.Net;
using System.Net.Http.Json;
using GameService.Models;
using GameService.Tests.TestFixtures;
using Shouldly;
using Xunit;

namespace GameService.Tests;

public class InitializeEndpointTests : IClassFixture<TestcontainersFixture>
{
    private readonly TestcontainersFixture _fixture;

    public InitializeEndpointTests(TestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Initialize_ResetsAndCreatesPlayer()
    {
        var client = _fixture.Client;

        // Initialize
        var initRes = await client.PostAsync("/initialize", null, TestContext.Current.CancellationToken);
        initRes.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Verify one player created
        var getAllAfterRes = await client.GetAsync("/player", TestContext.Current.CancellationToken);
        getAllAfterRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var playersAfter = await getAllAfterRes.Content.ReadFromJsonAsync<List<Player>>(TestContext.Current.CancellationToken);
        playersAfter.ShouldNotBeNull();
        playersAfter.Count.ShouldBe(1);

        // Verify that request was made to initialize recycler service
        var recyclerInitRequest = _fixture.HttpRequests.FirstOrDefault(r => r.RequestUri?.AbsolutePath == "/initialize" && r.Method == HttpMethod.Post);
        recyclerInitRequest.ShouldNotBeNull();
        recyclerInitRequest.RequestUri?.Host.ShouldBe("localhost");
        recyclerInitRequest.RequestUri?.Port.ShouldBe(5002);
    }
}