using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace RecyclerService.Tests;

public class ProgramPathsTests
{
    [Theory]
    [InlineData("Testing")]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Start_WithVariousEnvs_DoesNotThrow(string env)
    {
        var inMem = new Dictionary<string, string?>();
        inMem["ConnectionStrings:RecyclerConnection"] = env == "Testing" ? "DataSource=:memory:" : "";

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(env);
            builder.ConfigureAppConfiguration((ctx, conf) => { conf.AddInMemoryCollection(inMem); });
        });

        var client = factory.CreateClient();
        var res = await client.GetAsync("/", TestContext.Current.CancellationToken);
        res.ShouldNotBeNull();
    }
}