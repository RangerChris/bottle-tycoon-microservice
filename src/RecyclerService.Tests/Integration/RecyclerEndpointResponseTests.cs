using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using RecyclerService.Tests.TestFixtures;
using Shouldly;
using Xunit;

namespace RecyclerService.Tests.Integration;

public class RecyclerEndpointResponseTests : IAsyncLifetime
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
    public async Task CreateRecycler_ResponseContainsLocationHeader()
    {
        if (!_containers.IsAvailable)
        {
            return;
        }

        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var createRequest = new CreateRequest(Guid.NewGuid(), "Recycler Alpha", 120, "Sector 7");
        var response = await client.PostAsJsonAsync("/recyclers", createRequest, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.AbsolutePath.ShouldBe($"/recyclers/{createRequest.Id}");

        var body = await response.Content.ReadFromJsonAsync<CreateResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.Id.ShouldBe(createRequest.Id);
        body.Name.ShouldBe(createRequest.Name);
        body.Capacity.ShouldBe(createRequest.Capacity);
        body.Location.ShouldBe(createRequest.Location);
        body.CurrentLoad.ShouldBe(0);
    }

    [Fact]
    public async Task GetRecycler_NotFound_ReturnsErrorPayload()
    {
        if (!_containers.IsAvailable)
        {
            return;
        }

        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var res = await client.GetAsync($"/recyclers/{Guid.NewGuid()}", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var body = await res.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("Recycler not found", Case.Sensitive);
    }

    [Fact]
    public async Task VisitorArrived_NonexistentRecycler_ReturnsErrorPayload()
    {
        if (!_containers.IsAvailable)
        {
            return;
        }

        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var visitorRequest = new VisitorRequest { Bottles = 15, VisitorType = "WalkIn" };
        var res = await client.PostAsJsonAsync($"/recyclers/{Guid.NewGuid()}/visitors", visitorRequest, TestContext.Current.CancellationToken);

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var body = await res.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("Recycler not found", Case.Sensitive);
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, conf) =>
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
    }

    private sealed record CreateRequest(Guid Id, string Name, int Capacity, string? Location);

    private sealed record CreateResponse(Guid Id, string Name, int Capacity, int CurrentLoad, string? Location);

    private sealed record VisitorRequest
    {
        public int Bottles { get; set; }
        public string? VisitorType { get; set; }
    }
}