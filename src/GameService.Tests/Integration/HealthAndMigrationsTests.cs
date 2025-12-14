using System.Net;
using GameService.Tests.TestFixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace GameService.Tests.Integration;

public class HealthAndMigrationsTests
{
    [Fact]
    public async Task HealthReady_ShouldReturnOk_WhenDependenciesUp()
    {
        var containers = new TestcontainersFixture();
        try
        {
            await containers.InitializeAsync();

            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((_, conf) =>
                {
                    var connectionString = containers.Started
                        ? containers.Postgres.ConnectionString
                        : "Data Source=game_state_tests.db";

                    var cfg = new ConfigurationBuilder()
                        .AddInMemoryCollection(new[]
                        {
                            new KeyValuePair<string, string?>("ConnectionStrings:GameStateConnection", connectionString),
                            new KeyValuePair<string, string?>("APPLY_MIGRATIONS", "true"),
                            new KeyValuePair<string, string?>("ENABLE_MESSAGING", "false")
                        })
                        .Build();
                    conf.AddConfiguration(cfg);
                });
            });

            var client = factory.CreateClient();
            var res = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
            res.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        finally
        {
            await containers.DisposeAsync();
        }
    }
}