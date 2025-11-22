using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace ApiGateway.Tests;

public class RouteMatchingTests
{
    [Fact]
    public void ReverseProxyConfiguration_ShouldLoadRoutesCorrectly()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var reverseProxyConfig = config.GetSection("ReverseProxy");

        // Act
        var routes = reverseProxyConfig.GetSection("Routes").GetChildren();
        var clusters = reverseProxyConfig.GetSection("Clusters").GetChildren();

        // Assert
        routes.ShouldNotBeEmpty();
        clusters.ShouldNotBeEmpty();

        var routeNames = routes.Select(r => r.Key).ToList();
        routeNames.ShouldContain("gameservice");
        routeNames.ShouldContain("recyclerservice");
        routeNames.ShouldContain("truckservice");
        routeNames.ShouldContain("headquartersservice");
        routeNames.ShouldContain("recyclingplantservice");

        var clusterNames = clusters.Select(c => c.Key).ToList();
        clusterNames.ShouldContain("gameservice");
        clusterNames.ShouldContain("recyclerservice");
        clusterNames.ShouldContain("truckservice");
        clusterNames.ShouldContain("headquartersservice");
        clusterNames.ShouldContain("recyclingplantservice");
    }

    [Fact]
    public void GameServiceRoute_ShouldMatchCorrectPath()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var routes = config.GetSection("ReverseProxy:Routes").GetChildren();
        var gameServiceRoute = routes.First(r => r.Key == "gameservice");

        // Act
        var path = gameServiceRoute.GetSection("Match:Path").Value;
        var transforms = gameServiceRoute.GetSection("Transforms").GetChildren().ToList();

        // Assert
        path.ShouldBe("/api/gameservice/{**catch-all}");
        transforms.ShouldNotBeEmpty();
        transforms.First().GetSection("PathRemovePrefix").Value.ShouldBe("/api/gameservice");
    }

    [Fact]
    public void Clusters_ShouldHaveCorrectDestinations()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var clusters = config.GetSection("ReverseProxy:Clusters").GetChildren();

        // Act & Assert
        foreach (var cluster in clusters)
        {
            var destinations = cluster.GetSection("Destinations").GetChildren();
            destinations.ShouldNotBeEmpty();
            destinations.First().Key.ShouldBe("destination1");

            var address = destinations.First().GetSection("Address").Value;
            address.ShouldNotBeNullOrEmpty();
            address.ShouldStartWith("http://");
            address.ShouldEndWith(":80");
        }
    }
}