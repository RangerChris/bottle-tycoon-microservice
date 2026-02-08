using System.Net;
using System.Net.Http.Json;
using RecyclerService.Models;
using RecyclerService.Tests.TestFixtures;
using Shouldly;
using Xunit;

namespace RecyclerService.Tests;

public class InitializeEndpointTests : IClassFixture<TestcontainersFixture>
{
    private readonly TestcontainersFixture _fixture;

    public InitializeEndpointTests(TestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Initialize_ResetsAndCreatesRecycler()
    {
        var client = _fixture.Client;

        // Create some recyclers
        var createRes1 = await client.PostAsJsonAsync("/recyclers", new Recycler { Name = "R1", Capacity = 50 }, TestContext.Current.CancellationToken);
        createRes1.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createRes2 = await client.PostAsJsonAsync("/recyclers", new Recycler { Name = "R2", Capacity = 100 }, TestContext.Current.CancellationToken);
        createRes2.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Verify recyclers exist
        var getAllRes = await client.GetAsync("/recyclers", TestContext.Current.CancellationToken);
        getAllRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var recyclers = await getAllRes.Content.ReadFromJsonAsync<List<Recycler>>(TestContext.Current.CancellationToken);
        recyclers.ShouldNotBeNull();
        recyclers.Count.ShouldBeGreaterThan(0);

        // Initialize
        var initRes = await client.PostAsync("/initialize", null, TestContext.Current.CancellationToken);
        initRes.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Verify one recycler created
        var getAllAfterRes = await client.GetAsync("/recyclers", TestContext.Current.CancellationToken);
        getAllAfterRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var recyclersAfter = await getAllAfterRes.Content.ReadFromJsonAsync<List<Recycler>>(TestContext.Current.CancellationToken);
        recyclersAfter.ShouldNotBeNull();
        recyclersAfter.Count.ShouldBe(1);
    }
}