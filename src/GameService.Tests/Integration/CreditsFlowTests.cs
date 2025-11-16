using System.Net;
using System.Net.Http.Json;
using GameService.Models;
using GameService.Tests.TestFixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace GameService.Tests.Integration;

public class CreditsFlowTests
{
    [Fact]
    public async Task CreditEndpoint_ShouldPersistCredits()
    {
        var containers = new TestcontainersFixture();
        try
        {
            await containers.InitializeAsync();
            if (!containers.IsAvailable)
            {
                return;
            }

            var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, conf) =>
                {
                    var cfg = new ConfigurationBuilder()
                        .AddInMemoryCollection([
                            new KeyValuePair<string, string?>("ConnectionStrings:DefaultConnection", containers.Postgres.ConnectionString),
                            new KeyValuePair<string, string?>("ConnectionStrings:Redis", $"localhost:{containers.Redis.GetMappedPublicPort(6379)}")
                        ])
                        .Build();
                    conf.AddConfiguration(cfg);
                });
            });

            var client = factory.CreateClient();

            var createRes = await client.PostAsync("/players", null, TestContext.Current.CancellationToken);
            createRes.StatusCode.ShouldBe(HttpStatusCode.Created);
            var created = await createRes.Content.ReadFromJsonAsync<Player>(TestContext.Current.CancellationToken);
            created.ShouldNotBeNull();

            var creditRes = await client.PostAsJsonAsync($"/players/{created.Id}/credit", new { Amount = 50m, Reason = "test" }, TestContext.Current.CancellationToken);
            creditRes.StatusCode.ShouldBe(HttpStatusCode.OK);

            var getRes = await client.GetAsync($"/players/{created.Id}", TestContext.Current.CancellationToken);
            getRes.StatusCode.ShouldBe(HttpStatusCode.OK);
            var player = await getRes.Content.ReadFromJsonAsync<Player>(TestContext.Current.CancellationToken);
            player!.Credits.ShouldBe(50m);
        }
        finally
        {
            await containers.DisposeAsync();
        }
    }
}