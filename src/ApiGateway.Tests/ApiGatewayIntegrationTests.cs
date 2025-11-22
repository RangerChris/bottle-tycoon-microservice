using System.Net;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace ApiGateway.Tests;

public class ApiGatewayWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly IContainer _rabbitMqContainer;
    private readonly IContainer _redisContainer;

    public ApiGatewayWebApplicationFactory(IContainer redis, IContainer rabbit)
    {
        _redisContainer = redis;
        _rabbitMqContainer = rabbit;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddJsonFile("appsettings.json");
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = $"localhost:{_redisContainer.GetMappedPublicPort(6379)}",
                ["RabbitMQ:Host"] = $"localhost:{_rabbitMqContainer.GetMappedPublicPort(5672)}",
                ["RabbitMQ:Username"] = "guest",
                ["RabbitMQ:Password"] = "guest"
            });
        });
    }
}

public class ApiGatewayIntegrationTests : IAsyncLifetime
{
    private HttpClient? _client;
    private ApiGatewayWebApplicationFactory? _factory;
    private IContainer? _rabbitMqContainer;
    private IContainer? _redisContainer;

    public async ValueTask InitializeAsync()
    {
        // Start test containers
        _redisContainer = new ContainerBuilder()
            .WithImage("redis:7-alpine")
            .WithPortBinding(6379, true)
            .Build();

        _rabbitMqContainer = new ContainerBuilder()
            .WithImage("rabbitmq:3-management-alpine")
            .WithPortBinding(5672, true)
            .WithPortBinding(15672, true)
            .WithEnvironment("RABBITMQ_DEFAULT_USER", "guest")
            .WithEnvironment("RABBITMQ_DEFAULT_PASS", "guest")
            .Build();

        await _redisContainer.StartAsync(TestContext.Current.CancellationToken);
        await _rabbitMqContainer.StartAsync(TestContext.Current.CancellationToken);

        // Create test factory
        _factory = new ApiGatewayWebApplicationFactory(_redisContainer, _rabbitMqContainer);
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();

        if (_redisContainer != null)
        {
            await _redisContainer.StopAsync(TestContext.Current.CancellationToken);
        }

        if (_rabbitMqContainer != null)
        {
            await _rabbitMqContainer.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnOk()
    {
        // Act
        var response = await _client!.GetAsync("/health/live", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RootEndpoint_ShouldReturnApiGatewayOk()
    {
        // Act
        var response = await _client!.GetAsync("/", TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        content.ShouldBe("ApiGateway OK");
    }

    [Fact]
    public async Task RateLimit_ShouldBlockRequestsAfterLimit()
    {
        // Arrange - Make multiple requests quickly
        var tasks = new List<Task<HttpResponseMessage>>();
        for (var i = 0; i < 15; i++) // More than the 10 per second limit
        {
            tasks.Add(_client!.GetAsync("/", TestContext.Current.CancellationToken));
        }

        // Act
        var responses = await Task.WhenAll(tasks);

        // Assert
        var okResponses = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var rateLimitedResponses = responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests);

        // At least some should be rate limited
        rateLimitedResponses.ShouldBeGreaterThan(0);
        okResponses.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task SwaggerEndpoint_ShouldBeAccessible()
    {
        // Act
        var response = await _client!.GetAsync("/swagger", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReverseProxyRoutes_ShouldBeConfigured()
    {
        // This test verifies that the reverse proxy is set up
        // In a real scenario, we'd have mock services running
        // For now, just check that the endpoint exists and doesn't crash

        var response = await _client!.GetAsync("/api/gameservice/health", TestContext.Current.CancellationToken);
        // Should get a 502 or connection error since downstream isn't running
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadGateway, HttpStatusCode.ServiceUnavailable);
    }
}