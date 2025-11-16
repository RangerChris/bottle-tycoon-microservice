using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using RecyclerService.Tests.TestFixtures;
using Shouldly;
using Xunit;

namespace RecyclerService.Tests.Integration;

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
                    .AddInMemoryCollection([
                        new KeyValuePair<string, string?>("ConnectionStrings:RecyclerConnection", _containers.Postgres.ConnectionString),
                        new KeyValuePair<string, string?>("RabbitMQ:Host", $"localhost:{_containers.RabbitMq.GetMappedPublicPort(5672)}"),
                        new KeyValuePair<string, string?>("RabbitMQ:Username", "guest"),
                        new KeyValuePair<string, string?>("RabbitMQ:Password", "guest"),
                        new KeyValuePair<string, string?>("ENABLE_MESSAGING", "true")
                    ])
                    .Build();
                conf.AddConfiguration(cfg);
            });
        });

        var client = factory.CreateClient();
        var res = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<HealthResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.Status.ShouldBe("Healthy");
        body.Checks.ShouldContainKey("npgsql");
        body.Checks.ShouldContainKey("masstransit-bus");
    }

    [Fact]
    public async Task SwaggerJson_ShouldBeGenerated()
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
                    .AddInMemoryCollection([
                        new KeyValuePair<string, string?>("ConnectionStrings:RecyclerConnection", _containers.Postgres.ConnectionString),
                        new KeyValuePair<string, string?>("RabbitMQ:Host", $"localhost:{_containers.RabbitMq.GetMappedPublicPort(5672)}"),
                        new KeyValuePair<string, string?>("RabbitMQ:Username", "guest"),
                        new KeyValuePair<string, string?>("RabbitMQ:Password", "guest"),
                        new KeyValuePair<string, string?>("ENABLE_MESSAGING", "true")
                    ])
                    .Build();
                conf.AddConfiguration(cfg);
            });
        });

        var client = factory.CreateClient();
        var res = await client.GetAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await res.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        json.ShouldNotBeNullOrWhiteSpace();

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("paths", out var paths).ShouldBeTrue();
        doc.RootElement.TryGetProperty("components", out var comps).ShouldBeTrue();
    }

    public sealed record HealthResponse(string Status, Dictionary<string, string> Checks);
}