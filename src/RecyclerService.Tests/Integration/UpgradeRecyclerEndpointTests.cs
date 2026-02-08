using System.Net;
using System.Net.Http.Json;
using RecyclerService.Endpoints;
using RecyclerService.Tests.TestFixtures;
using Shouldly;
using Xunit;

namespace RecyclerService.Tests.Integration;

public class UpgradeRecyclerEndpointTests : IClassFixture<TestcontainersFixture>
{
    private readonly TestcontainersFixture _fixture;

    public UpgradeRecyclerEndpointTests(TestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UpgradeRecycler_ShouldUpgradeSuccessfully()
    {
        var client = _fixture.Client;

        // Create a recycler
        var recyclerId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var createRequest = new CreateRequest(recyclerId, "Upgrade Test Recycler", 100, "Zone A");
        var createResponse = await client.PostAsJsonAsync("/recyclers", createRequest, TestContext.Current.CancellationToken);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Upgrade the recycler
        var upgradeRequest = new UpgradeRecyclerEndpoint.Request
        {
            PlayerId = playerId,
            RecyclerId = recyclerId
        };
        var upgradeResponse = await client.PostAsJsonAsync($"/recyclers/{recyclerId}/upgrade", upgradeRequest, TestContext.Current.CancellationToken);
        upgradeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var upgradeResult = await upgradeResponse.Content.ReadFromJsonAsync<UpgradeRecyclerEndpoint.Response>(TestContext.Current.CancellationToken);
        upgradeResult.ShouldNotBeNull();
        upgradeResult.Id.ShouldBe(recyclerId);
        upgradeResult.Name.ShouldBe("Upgrade Test Recycler");
        upgradeResult.CapacityLevel.ShouldBe(1);
        upgradeResult.Capacity.ShouldBe(125); // 100 * 1.25^1
        upgradeResult.CurrentLoad.ShouldBe(0);
        upgradeResult.Location.ShouldBe("Zone A");
    }

    [Fact]
    public async Task UpgradeRecycler_RecyclerNotFound_ShouldReturn404()
    {
        var client = _fixture.Client;

        var upgradeRequest = new UpgradeRecyclerEndpoint.Request
        {
            PlayerId = Guid.NewGuid(),
            RecyclerId = Guid.NewGuid()
        };
        var response = await client.PostAsJsonAsync($"/recyclers/{upgradeRequest.RecyclerId}/upgrade", upgradeRequest, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpgradeRecycler_AlreadyAtMaxLevel_ShouldReturn400()
    {
        var client = _fixture.Client;

        // Create a recycler
        var recyclerId = Guid.NewGuid();
        var createRequest = new CreateRequest(recyclerId, "Max Level Recycler", 100, "Zone B");
        var createResponse = await client.PostAsJsonAsync("/recyclers", createRequest, TestContext.Current.CancellationToken);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Upgrade to level 1
        var upgradeRequest1 = new UpgradeRecyclerEndpoint.Request
        {
            PlayerId = Guid.NewGuid(),
            RecyclerId = recyclerId
        };
        await client.PostAsJsonAsync($"/recyclers/{recyclerId}/upgrade", upgradeRequest1, TestContext.Current.CancellationToken);

        // Upgrade to level 2
        var upgradeRequest2 = new UpgradeRecyclerEndpoint.Request
        {
            PlayerId = Guid.NewGuid(),
            RecyclerId = recyclerId
        };
        await client.PostAsJsonAsync($"/recyclers/{recyclerId}/upgrade", upgradeRequest2, TestContext.Current.CancellationToken);

        // Upgrade to level 3
        var upgradeRequest3 = new UpgradeRecyclerEndpoint.Request
        {
            PlayerId = Guid.NewGuid(),
            RecyclerId = recyclerId
        };
        await client.PostAsJsonAsync($"/recyclers/{recyclerId}/upgrade", upgradeRequest3, TestContext.Current.CancellationToken);

        // Try to upgrade again (should fail)
        var upgradeRequest4 = new UpgradeRecyclerEndpoint.Request
        {
            PlayerId = Guid.NewGuid(),
            RecyclerId = recyclerId
        };
        var response = await client.PostAsJsonAsync($"/recyclers/{recyclerId}/upgrade", upgradeRequest4, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpgradeRecycler_ShouldSendCorrectDebitRequest()
    {
        var client = _fixture.Client;

        // Create a recycler
        var recyclerId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var createRequest = new CreateRequest(recyclerId, "Debit Test Recycler", 100, "Zone C");
        var createResponse = await client.PostAsJsonAsync("/recyclers", createRequest, TestContext.Current.CancellationToken);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Clear previous requests
        _fixture.HttpRequests.Clear();

        // Upgrade the recycler
        var upgradeRequest = new UpgradeRecyclerEndpoint.Request
        {
            PlayerId = playerId,
            RecyclerId = recyclerId
        };
        var upgradeResponse = await client.PostAsJsonAsync($"/recyclers/{recyclerId}/upgrade", upgradeRequest, TestContext.Current.CancellationToken);
        upgradeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Check that a debit request was made
        _fixture.HttpRequests.ShouldHaveSingleItem();
        var request = _fixture.HttpRequests[0];
        request.RequestUri?.AbsolutePath.ShouldBe($"/player/{playerId}/deduct");
        request.Method.ShouldBe(HttpMethod.Post);
    }

    private sealed record CreateRequest(Guid Id, string Name, int Capacity, string? Location);
}