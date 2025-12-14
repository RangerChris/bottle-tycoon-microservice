using System.Net;
using System.Net.Http.Json;
using GameService.Tests.TestFixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace GameService.Tests.Integration;

public class HealthEndpointTests : IAsyncLifetime
{
    private readonly TestcontainersFixture _containers = new();

    public ValueTask InitializeAsync()
    {
        return _containers.InitializeAsync();
    }

    public ValueTask DisposeAsync()
    {
        return _containers.DisposeAsync();
    }

    [Fact]
    public async Task HealthEndpoint_ShouldReturnHealthyJson()
    {
        if (!_containers.IsAvailable)
        {
            return;
        }

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((context, conf) =>
            {
                var cfg = new ConfigurationBuilder()
                    .AddInMemoryCollection(new[]
                    {
                        new KeyValuePair<string, string?>("ConnectionStrings:GameStateConnection", _containers.Postgres.ConnectionString)
                    })
                    .Build();
                conf.AddConfiguration(cfg);
            });
        });

        var client = factory.CreateClient();
        var res = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await res.Content.ReadFromJsonAsync<dynamic>(TestContext.Current.CancellationToken);
        ((string)content!.status).ShouldBe("Healthy");
    }
}