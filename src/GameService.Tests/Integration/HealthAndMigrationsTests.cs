using System.Net;
using GameService.Tests.TestFixtures;
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
            var res = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
            res.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        finally
        {
            await containers.DisposeAsync();
        }
    }
}