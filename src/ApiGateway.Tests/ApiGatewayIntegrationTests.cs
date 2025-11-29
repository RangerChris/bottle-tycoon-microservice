using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace ApiGateway.Tests;

public class ApiGatewayIntegrationTests : IAsyncLifetime
{
    private readonly Dictionary<string, int> _servicePorts = new();
    private readonly List<IWebHost> _stubHosts = new();
    private HttpClient? _client;
    private WebApplicationFactory<Program>? _factory;

    public async ValueTask InitializeAsync()
    {
        // Start stub downstream services (simple Kestrel hosts)
        var services = new[] { "gameservice", "recyclerservice", "truckservice", "headquartersservice", "recyclingplantservice" };
        foreach (var svc in services)
        {
            var port = GetFreeTcpPort();
            _servicePorts[svc] = port;
            var host = new WebHostBuilder()
                .UseKestrel()
                .UseUrls($"http://127.0.0.1:{port}")
                .Configure(app =>
                {
                    // Simple middleware-based routing to avoid MapGet extension resolution issues
                    app.Run(async ctx =>
                    {
                        var path = ctx.Request.Path.Value ?? string.Empty;
                        if (path == "/")
                        {
                            await ctx.Response.WriteAsync($"{svc} OK");
                            return;
                        }

                        if (path.Equals("/health/live", StringComparison.OrdinalIgnoreCase) ||
                            path.Equals("/health/ready", StringComparison.OrdinalIgnoreCase))
                        {
                            ctx.Response.ContentType = "text/plain";
                            await ctx.Response.WriteAsync("Healthy");
                            return;
                        }

                        ctx.Response.StatusCode = 404;
                    });
                })
                .Build();
            await host.StartAsync();
            _stubHosts.Add(host);
        }

        CreateFactory();

        _client = _factory?.CreateClient();
    }

    private void CreateFactory()
    {
        // Create WebApplicationFactory for ApiGateway and override configuration
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, conf) =>
            {
                var dict = new Dictionary<string, string?>();
                // Override reverse proxy cluster destinations to point to the stub hosts
                foreach (var kv in _servicePorts)
                {
                    var clusterKey = $"ReverseProxy:Clusters:{kv.Key}:Destinations:destination1:Address";
                    dict[clusterKey] = $"http://localhost:{kv.Value}";
                }

                // Avoid setting external Redis/RabbitMQ connections here; health checks are replaced below
                conf.AddInMemoryCollection(dict);
            });

            // Replace health checks for Redis and RabbitMQ to simple healthy checks to avoid dependency timing issues
            builder.ConfigureServices(dummyServices => { dummyServices.AddHealthChecks().AddCheck("dummy", () => HealthCheckResult.Healthy()); });
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_client != null)
        {
            _client.Dispose();
        }

        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }

        foreach (var host in _stubHosts)
        {
            try
            {
                await host.StopAsync();
            }
            catch
            {
                // ignored
            }

            host.Dispose();
        }

        _stubHosts.Clear();
    }

    [Fact]
    public async Task Gateway_swagger_available()
    {
        var resp = await _client!.GetAsync("/swagger", TestContext.Current.CancellationToken);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("swagger-ui", body);
    }

    [Fact]
    public async Task Gateway_metrics_available()
    {
        var resp = await _client!.GetAsync("/metrics", TestContext.Current.CancellationToken);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("# TYPE", body);
    }

    [Theory]
    [InlineData("gameservice")]
    [InlineData("recyclerservice")]
    [InlineData("truckservice")]
    [InlineData("headquartersservice")]
    [InlineData("recyclingplantservice")]
    public async Task Proxied_service_health_live(string service)
    {
        var resp = await _client!.GetAsync($"/api/{service}/health/live", TestContext.Current.CancellationToken);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Healthy", body);
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}