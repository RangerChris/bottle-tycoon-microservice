using System.Net;
using System.Net.Http.Json;
using GameService.Models;
using GameService.Tests.TestFixtures;
using Shouldly;
using Xunit;

namespace GameService.Tests;

public class AllEndpointsCoverageTests : IClassFixture<TestcontainersFixture>
{
    private readonly TestcontainersFixture _fixture;

    public AllEndpointsCoverageTests(TestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AllEndpoints_BasicFlow_ShouldSucceedAndReturnExpectedStatuses()
    {
        var client = _fixture.Client;

        // Health
        var health = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        health.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Create player
        var createRes = await client.PostAsJsonAsync("/player", new Player(), TestContext.Current.CancellationToken);
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<Player>(TestContext.Current.CancellationToken);
        created.ShouldNotBeNull();

        // Get player
        var getRes = await client.GetAsync($"/player/{created.Id}", TestContext.Current.CancellationToken);
        getRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var got = await getRes.Content.ReadFromJsonAsync<Player>(TestContext.Current.CancellationToken);
        got.ShouldNotBeNull();
        got.Id.ShouldBe(created.Id);

        // Get all players
        var getAllRes = await client.GetAsync("/player", TestContext.Current.CancellationToken);
        getAllRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var players = await getAllRes.Content.ReadFromJsonAsync<List<Player>>(TestContext.Current.CancellationToken);
        players.ShouldNotBeNull();
        players.ShouldContain(p => p.Id == created.Id);

        // Deposit invalid amount
        var badDeposit = await client.PostAsJsonAsync($"/player/{created.Id}/deposit", new { Amount = -10m, Reason = "bad" }, TestContext.Current.CancellationToken);
        badDeposit.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var badDepositMsg = await EndpointTestsHelpers.ReadFirstErrorMessage(badDeposit);
        badDepositMsg.ShouldContain("Amount must be positive");

        // Valid deposit
        var deposit = await client.PostAsJsonAsync($"/player/{created.Id}/deposit", new { Amount = 50m, Reason = "test" }, TestContext.Current.CancellationToken);
        deposit.StatusCode.ShouldBe(HttpStatusCode.OK);
        var depositResult = await deposit.Content.ReadFromJsonAsync<bool>(TestContext.Current.CancellationToken);
        depositResult.ShouldBeTrue();

        // Deduct invalid amount
        var badDeduct = await client.PostAsJsonAsync($"/player/{created.Id}/deduct", new { Amount = -5m, Reason = "bad" }, TestContext.Current.CancellationToken);
        badDeduct.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var badDeductMsg = await EndpointTestsHelpers.ReadFirstErrorMessage(badDeduct);
        badDeductMsg.ShouldContain("Amount must be positive");

        // Valid deduct
        var deduct = await client.PostAsJsonAsync($"/player/{created.Id}/deduct", new { Amount = 10m, Reason = "test" }, TestContext.Current.CancellationToken);
        deduct.StatusCode.ShouldBe(HttpStatusCode.OK);
        var deductResult = await deduct.Content.ReadFromJsonAsync<bool>(TestContext.Current.CancellationToken);
        deductResult.ShouldBeTrue();
    }
}