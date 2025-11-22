using AspNetCoreRateLimit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace ApiGateway.Tests;

public class RateLimitLogicTests
{
    [Fact]
    public void IpRateLimitOptions_ShouldBeConfiguredCorrectly()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var options = new IpRateLimitOptions();
        config.GetSection("IpRateLimiting").Bind(options);

        // Assert
        options.EnableEndpointRateLimiting.ShouldBeTrue();
        options.HttpStatusCode.ShouldBe(429);
        options.GeneralRules.ShouldNotBeEmpty();
        options.GeneralRules.Count.ShouldBe(2);

        var firstRule = options.GeneralRules[0];
        firstRule.Endpoint.ShouldBe("*");
        firstRule.Period.ShouldBe("1s");
        firstRule.Limit.ShouldBe(10);

        var secondRule = options.GeneralRules[1];
        secondRule.Endpoint.ShouldBe("*");
        secondRule.Period.ShouldBe("15m");
        secondRule.Limit.ShouldBe(100);
    }

    [Fact]
    public void EndpointWhitelist_ShouldContainHealthEndpoints()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var options = new IpRateLimitOptions();
        config.GetSection("IpRateLimiting").Bind(options);

        // Assert
        options.EndpointWhitelist.ShouldNotBeEmpty();
        options.EndpointWhitelist.ShouldContain("get:/api/health");
        options.EndpointWhitelist.ShouldContain("get:/health");
    }

    [Fact]
    public void RateLimitServices_ShouldBeRegistered()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddMemoryCache();
        services.Configure<IpRateLimitOptions>(new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build()
            .GetSection("IpRateLimiting"));
        services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
        services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
        services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
        services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();

        // Act
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.GetService<IIpPolicyStore>().ShouldNotBeNull();
        serviceProvider.GetService<IRateLimitCounterStore>().ShouldNotBeNull();
        serviceProvider.GetService<IRateLimitConfiguration>().ShouldNotBeNull();
        serviceProvider.GetService<IProcessingStrategy>().ShouldNotBeNull();
    }
}