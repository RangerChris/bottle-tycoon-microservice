using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace HeadquartersService.Tests;

public class ProgramPathsTests
{
    [Theory]
    [InlineData("Testing")]
    [InlineData("Development")]
    public async Task Startup_VariousEnvs_NoThrow(string env)
    {
        var inMem = new Dictionary<string, string?>();
        inMem["ENABLE_MESSAGING"] = "false";

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