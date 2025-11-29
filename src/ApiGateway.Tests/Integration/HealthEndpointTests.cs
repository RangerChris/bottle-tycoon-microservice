using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace ApiGateway.Tests.Integration;

public class HealthEndpointTests
{
    [Fact]
    public async Task HealthEndpoint_AggregatesServiceStatuses()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, conf) =>
            {
                var cfg = new ConfigurationBuilder().AddInMemoryCollection([
                    new KeyValuePair<string, string?>("ENABLE_MESSAGING", "false")
                ]).Build();
                conf.AddConfiguration(cfg);
            });

            builder.ConfigureServices(services =>
            {
                // Replace IHttpClientFactory with one that returns a HttpClient using a fake handler
                services.AddSingleton<IHttpClientFactory>(_ =>
                {
                    var handler = new FakeHealthHandler();
                    var client = new HttpClient(handler)
                    {
                        BaseAddress = new Uri("http://localhost")
                    };
                    return new SimpleFactory(client);
                });
            });
        });

        var client = factory.CreateClient();
        var res = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.ValueKind.ShouldBe(JsonValueKind.Object);
        body.TryGetProperty("status", out var statusEl).ShouldBeTrue();
        statusEl.GetString().ShouldBe("Healthy");
        body.TryGetProperty("services", out var servicesEl).ShouldBeTrue();
        servicesEl.ValueKind.ShouldBe(JsonValueKind.Object);

        // All services should report Healthy in the fake handler
        foreach (var svc in new[] { "gameservice", "recyclerservice", "truckservice", "headquartersservice", "recyclingplantservice" })
        {
            servicesEl.TryGetProperty(svc, out var svcEl).ShouldBeTrue();
            svcEl.GetString().ShouldBe("Healthy");
        }
    }

    private class SimpleFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public SimpleFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name)
        {
            return _client;
        }
    }

    private class FakeHealthHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Respond with 200 OK for any /health/live path
            var msg = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { status = "Healthy" })
            };
            return Task.FromResult(msg);
        }
    }
}