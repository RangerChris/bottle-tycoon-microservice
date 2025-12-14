using System.Net;
using System.Net.Http.Json;
using GameService.Models;
using GameService.Tests.TestFixtures;
using Serilog;
using Shouldly;
using Xunit;

namespace GameService.Tests.Integration;

public class CreditsFlowTests : IClassFixture<TestcontainersFixture>
{
    private readonly TestcontainersFixture _fixture;

    public CreditsFlowTests(TestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreditEndpoint_ShouldPersistCredits()
    {
        var client = _fixture.Client;

        var createRes = await client.PostAsJsonAsync("/player", new { }, TestContext.Current.CancellationToken);
        if (createRes.StatusCode != HttpStatusCode.Created)
        {
            var content = await createRes.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Log.Error("CreatePlayer failed: {CreateResStatusCode}, Content: {Content}", createRes.StatusCode, content);
        }
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<Player>(TestContext.Current.CancellationToken);
        created.ShouldNotBeNull();

        var creditRes = await client.PostAsJsonAsync($"/player/{created.Id}/deposit", new { Amount = 50m, Reason = "test" }, TestContext.Current.CancellationToken);
        creditRes.StatusCode.ShouldBe(HttpStatusCode.OK);

        var getRes = await client.GetAsync($"/player/{created.Id}", TestContext.Current.CancellationToken);
        getRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var player = await getRes.Content.ReadFromJsonAsync<Player>(TestContext.Current.CancellationToken);
        player!.Credits.ShouldBe(1050m);
    }
}