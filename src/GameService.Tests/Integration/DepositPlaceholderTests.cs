using System.Net;
using System.Net.Http.Json;
using GameService.Models;
using GameService.Tests.TestFixtures;
using Shouldly;
using Xunit;

namespace GameService.Tests.Integration;

public class DepositPlaceholderTests : IClassFixture<TestcontainersFixture>
{
    private readonly TestcontainersFixture _fixture;

    public DepositPlaceholderTests(TestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Deposit_WithValidGuid_Succeeds_And_LiteralPlaceholder_ReturnsNotFound()
    {
        var client = _fixture.Client;
        var token = TestContext.Current.CancellationToken;

        // Create player
        var createRes = await client.PostAsJsonAsync("/player", new Player(), token);
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<Player>(token);
        created.ShouldNotBeNull();

        // Valid deposit should succeed
        var depositReq = new { Amount = 50m, Reason = "test" };
        var depositRes = await client.PostAsJsonAsync($"/player/{created.Id}/deposit", depositReq, token);
        depositRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var depositResult = await depositRes.Content.ReadFromJsonAsync<bool>(token);
        depositResult.ShouldBeTrue();

        // Sending the literal placeholder in the path should not match the route (route constraint :guid)
        var badRes = await client.PostAsJsonAsync("/player/{PlayerId}/deposit", depositReq, token);
        badRes.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Deduct_WithValidGuid_Succeeds_And_LiteralPlaceholder_ReturnsNotFound()
    {
        var client = _fixture.Client;
        var token = TestContext.Current.CancellationToken;

        // Create player
        var createRes = await client.PostAsJsonAsync("/player", new Player(), token);
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<Player>(token);
        created.ShouldNotBeNull();

        // Valid deduct should succeed
        var deductReq = new { Amount = 10m, Reason = "test deduct" };
        var deductRes = await client.PostAsJsonAsync($"/player/{created.Id}/deduct", deductReq, token);
        deductRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var deductResult = await deductRes.Content.ReadFromJsonAsync<bool>(token);
        deductResult.ShouldBeTrue();

        // Sending the literal placeholder in the path should not match the route (route constraint :guid)
        var badRes = await client.PostAsJsonAsync("/player/{PlayerId}/deduct", deductReq, token);
        badRes.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}