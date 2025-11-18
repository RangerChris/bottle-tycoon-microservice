using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using RecyclerService.Tests.TestFixtures;
using Shouldly;
using Xunit;

namespace RecyclerService.Tests.Integration;

public class CreateGetRecyclerEndpointTests : IAsyncLifetime
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
    public async Task CreateRecycler_ShouldCreateAndReturnRecycler()
    {
        if (!_containers.IsAvailable)
        {
            return;
        }

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((context, conf) =>
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

        var client = factory.CreateClient();

        var createRequest = new CreateRequest(Guid.NewGuid(), "Test Recycler", 100, "Test Location");
        var createRes = await client.PostAsJsonAsync("/recyclers", createRequest, TestContext.Current.CancellationToken);

        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createBody = await createRes.Content.ReadFromJsonAsync<CreateResponse>(TestContext.Current.CancellationToken);
        createBody.ShouldNotBeNull();
        createBody.Id.ShouldBe(createRequest.Id);
        createBody.Name.ShouldBe(createRequest.Name);
        createBody.Capacity.ShouldBe(createRequest.Capacity);
        createBody.CurrentLoad.ShouldBe(0);
        createBody.Location.ShouldBe(createRequest.Location);

        // Now get the recycler
        var getRes = await client.GetAsync($"/recyclers/{createBody.Id}", TestContext.Current.CancellationToken);
        getRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var getBody = await getRes.Content.ReadFromJsonAsync<GetResponse>(TestContext.Current.CancellationToken);
        getBody.ShouldNotBeNull();
        getBody.Id.ShouldBe(createBody.Id);
        getBody.Name.ShouldBe(createBody.Name);
        getBody.Capacity.ShouldBe(createBody.Capacity);
        getBody.CurrentLoad.ShouldBe(createBody.CurrentLoad);
    }

    [Fact]
    public async Task VisitorArrived_ShouldIncreaseLoadAndReturnUpdatedRecycler()
    {
        if (!_containers.IsAvailable)
        {
            return;
        }

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((context, conf) =>
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

        var client = factory.CreateClient();

        // Create a recycler first
        var createRequest = new CreateRequest(Guid.NewGuid(), "Test Recycler", 100, "Test Location");
        var createRes = await client.PostAsJsonAsync("/recyclers", createRequest, TestContext.Current.CancellationToken);
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createBody = await createRes.Content.ReadFromJsonAsync<CreateResponse>(TestContext.Current.CancellationToken);
        createBody.ShouldNotBeNull();
        createBody.CurrentLoad.ShouldBe(0);

        // Visitor arrives with 25 bottles
        var visitorRequest = new VisitorRequest { Bottles = 25, VisitorType = "Regular" };
        var visitorRes = await client.PostAsJsonAsync($"/recyclers/{createBody.Id}/visitors", visitorRequest, TestContext.Current.CancellationToken);
        visitorRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var visitorBody = await visitorRes.Content.ReadFromJsonAsync<VisitorResponse>(TestContext.Current.CancellationToken);
        visitorBody.ShouldNotBeNull();
        visitorBody.RecyclerId.ShouldBe(createBody.Id);
        visitorBody.CurrentLoad.ShouldBe(25);
        visitorBody.Capacity.ShouldBe(100);

        // Now get the recycler
        var getRes = await client.GetAsync($"/recyclers/{createBody.Id}", TestContext.Current.CancellationToken);
        getRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var getBody = await getRes.Content.ReadFromJsonAsync<GetResponse>(TestContext.Current.CancellationToken);
        getBody.ShouldNotBeNull();
        getBody.Id.ShouldBe(createBody.Id);
        getBody.Name.ShouldBe(createBody.Name);
        getBody.Capacity.ShouldBe(createBody.Capacity);
        getBody.CurrentLoad.ShouldBe(25);
    }

    [Fact]
    public async Task VisitorArrived_WithNonExistentRecycler_ShouldReturn404()
    {
        if (!_containers.IsAvailable)
        {
            return;
        }

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((context, conf) =>
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

        var client = factory.CreateClient();

        // Try to add visitor to non-existent recycler
        var visitorRequest = new VisitorRequest { Bottles = 10, VisitorType = "Regular" };
        var visitorRes = await client.PostAsJsonAsync($"/recyclers/{Guid.NewGuid()}/visitors", visitorRequest, TestContext.Current.CancellationToken);
        visitorRes.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MultipleVisitors_ShouldAccumulateLoadCorrectly()
    {
        if (!_containers.IsAvailable)
        {
            return;
        }

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((context, conf) =>
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

        var client = factory.CreateClient();

        // Create a recycler first
        var createRequest = new CreateRequest(Guid.NewGuid(), "Test Recycler", 100, "Test Location");
        var createRes = await client.PostAsJsonAsync("/recyclers", createRequest, TestContext.Current.CancellationToken);
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createBody = await createRes.Content.ReadFromJsonAsync<CreateResponse>(TestContext.Current.CancellationToken);
        createBody.ShouldNotBeNull();

        // First visitor with 20 bottles
        var visitor1Request = new VisitorRequest { Bottles = 20, VisitorType = "Regular" };
        var visitor1Res = await client.PostAsJsonAsync($"/recyclers/{createBody.Id}/visitors", visitor1Request, TestContext.Current.CancellationToken);
        visitor1Res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var visitor1Body = await visitor1Res.Content.ReadFromJsonAsync<VisitorResponse>(TestContext.Current.CancellationToken);
        visitor1Body.ShouldNotBeNull();
        visitor1Body.CurrentLoad.ShouldBe(20);

        // Second visitor with 30 bottles
        var visitor2Request = new VisitorRequest { Bottles = 30, VisitorType = "Premium" };
        var visitor2Res = await client.PostAsJsonAsync($"/recyclers/{createBody.Id}/visitors", visitor2Request, TestContext.Current.CancellationToken);
        visitor2Res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var visitor2Body = await visitor2Res.Content.ReadFromJsonAsync<VisitorResponse>(TestContext.Current.CancellationToken);
        visitor2Body.ShouldNotBeNull();
        visitor2Body.CurrentLoad.ShouldBe(50);

        // Verify final state
        var getRes = await client.GetAsync($"/recyclers/{createBody.Id}", TestContext.Current.CancellationToken);
        getRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var getBody = await getRes.Content.ReadFromJsonAsync<GetResponse>(TestContext.Current.CancellationToken);
        getBody.ShouldNotBeNull();
        getBody.CurrentLoad.ShouldBe(50);
    }

    public sealed record CreateRequest(Guid Id, string Name, int Capacity, string? Location);

    public sealed record CreateResponse(Guid Id, string Name, int Capacity, int CurrentLoad, string? Location);

    public sealed record GetResponse(Guid Id, string Name, int CurrentLoad, int Capacity);

    public sealed record VisitorRequest
    {
        public int Bottles { get; set; }
        public string? VisitorType { get; set; }
    }

    public sealed record VisitorResponse
    {
        public Guid RecyclerId { get; set; }
        public int CurrentLoad { get; set; }
        public int Capacity { get; set; }
    }
}