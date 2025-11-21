using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace GameService.Tests;

public class ProgramPathsTests
{
    [Theory]
    [InlineData("Testing", true, true)]
    [InlineData("Development", true, false)]
    [InlineData("Production", false, true)]
    public async Task Start_WithVariousConfigs_DoesNotThrow(string env, bool enableMessaging, bool hasConnection)
    {
        var inMem = new Dictionary<string, string?>();
        inMem["ENABLE_MESSAGING"] = enableMessaging ? "true" : "false";
        if (hasConnection)
        {
            inMem["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=bt;Username=bt;Password=bt";
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(env);
            builder.ConfigureAppConfiguration((ctx, conf) => { conf.AddInMemoryCollection(inMem); });
        });

        var client = factory.CreateClient();
        var res = await client.GetAsync("/", TestContext.Current.CancellationToken);
        res.ShouldNotBeNull();

        var live = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);
        live.ShouldNotBeNull();
    }
}