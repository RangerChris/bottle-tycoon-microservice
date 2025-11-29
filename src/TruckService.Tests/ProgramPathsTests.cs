using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Shouldly;
using Xunit;

namespace TruckService.Tests;

public class ProgramPathsTests
{
    [Theory]
    [InlineData("Testing")]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Start_WithEnvironments_Works(string env)
    {
        var inMem = new Dictionary<string, string?>
        {
            ["ENABLE_MESSAGING"] = "false",
            ["ConnectionStrings:Redis"] = "localhost:6379"
        };
        if (env != "Testing")
        {
            inMem["ConnectionStrings:DefaultConnection"] = "";
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(env);
            builder.ConfigureAppConfiguration((ctx, conf) =>
            {
                conf.Sources.Insert(0, new MemoryConfigurationSource { InitialData = inMem });
            });
        });

        var client = factory.CreateClient();
        var res = await client.GetAsync("/", TestContext.Current.CancellationToken);
        res.ShouldNotBeNull();
    }
}