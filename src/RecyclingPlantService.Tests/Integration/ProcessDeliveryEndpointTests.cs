using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RecyclingPlantService.Data;
using RecyclingPlantService.Tests.TestFixtures;
using Shouldly;
using Xunit;

namespace RecyclingPlantService.Tests.Integration;

public class ProcessDeliveryEndpointTests : IClassFixture<TestcontainersFixture>
{
    private readonly TestcontainersFixture _fixture;

    public ProcessDeliveryEndpointTests(TestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ProcessDelivery_WithValidData_CreatesDeliveryRecord()
    {
        var client = _fixture.Client;
        var truckId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        var request = new ProcessDeliveryRequest
        {
            TruckId = truckId,
            PlayerId = playerId,
            LoadByType = new Dictionary<string, int>
            {
                { "glass", 10 },
                { "metal", 5 },
                { "plastic", 8 }
            },
            OperatingCost = 10m
        };

        var response = await client.PostAsJsonAsync("/deliveries", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProcessDeliveryResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.Success.ShouldBeTrue();
        body.DeliveryId.ShouldNotBeNull();
        body.GrossEarnings.ShouldBe(66.5m);
        body.NetEarnings.ShouldBe(56.5m);
    }

    [Fact]
    public async Task ProcessDelivery_CalculatesCorrectEarnings()
    {
        var client = _fixture.Client;

        var request = new ProcessDeliveryRequest
        {
            TruckId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            LoadByType = new Dictionary<string, int>
            {
                { "glass", 20 },
                { "metal", 10 },
                { "plastic", 15 }
            },
            OperatingCost = 20m
        };

        var response = await client.PostAsJsonAsync("/deliveries", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProcessDeliveryResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.GrossEarnings.ShouldBe(131.25m);
        body.NetEarnings.ShouldBe(111.25m);
    }

    [Fact]
    public async Task ProcessDelivery_WithEmptyTruckId_ReturnsBadRequest()
    {
        var client = _fixture.Client;

        var request = new ProcessDeliveryRequest
        {
            TruckId = Guid.Empty,
            PlayerId = Guid.NewGuid(),
            LoadByType = new Dictionary<string, int> { { "glass", 10 } },
            OperatingCost = 5m
        };

        var response = await client.PostAsJsonAsync("/deliveries", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ProcessDelivery_WithEmptyPlayerId_ReturnsBadRequest()
    {
        var client = _fixture.Client;

        var request = new ProcessDeliveryRequest
        {
            TruckId = Guid.NewGuid(),
            PlayerId = Guid.Empty,
            LoadByType = new Dictionary<string, int> { { "glass", 10 } },
            OperatingCost = 5m
        };

        var response = await client.PostAsJsonAsync("/deliveries", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ProcessDelivery_WithEmptyLoad_ReturnsBadRequest()
    {
        var client = _fixture.Client;

        var request = new ProcessDeliveryRequest
        {
            TruckId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            LoadByType = new Dictionary<string, int>(),
            OperatingCost = 5m
        };

        var response = await client.PostAsJsonAsync("/deliveries", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ProcessDelivery_WithZeroOperatingCost_StillProcesses()
    {
        var client = _fixture.Client;

        var request = new ProcessDeliveryRequest
        {
            TruckId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            LoadByType = new Dictionary<string, int>
            {
                { "glass", 5 }
            },
            OperatingCost = 0m
        };

        var response = await client.PostAsJsonAsync("/deliveries", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProcessDeliveryResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.GrossEarnings.ShouldBe(20m);
        body.NetEarnings.ShouldBe(20m);
    }

    [Fact]
    public async Task ProcessDelivery_PersistsToDatabase()
    {
        var client = _fixture.Client;
        var truckId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        var request = new ProcessDeliveryRequest
        {
            TruckId = truckId,
            PlayerId = playerId,
            LoadByType = new Dictionary<string, int>
            {
                { "glass", 12 },
                { "metal", 8 },
                { "plastic", 6 }
            },
            OperatingCost = 15m
        };

        var response = await client.PostAsJsonAsync("/deliveries", request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProcessDeliveryResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();

        using (var scope = _fixture.Host!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecyclingPlantDbContext>();
            var delivery = await db.PlantDeliveries.FindAsync(new object?[] { body.DeliveryId }, TestContext.Current.CancellationToken);
            delivery.ShouldNotBeNull();
            delivery.TruckId.ShouldBe(truckId);
            delivery.PlayerId.ShouldBe(playerId);
            delivery.GlassCount.ShouldBe(12);
            delivery.MetalCount.ShouldBe(8);
            delivery.PlasticCount.ShouldBe(6);
            delivery.OperatingCost.ShouldBe(15m);
        }
    }

    [Fact]
    public async Task ProcessDelivery_WithOnlyGlass_CalculatesCorrectly()
    {
        var client = _fixture.Client;

        var request = new ProcessDeliveryRequest
        {
            TruckId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            LoadByType = new Dictionary<string, int>
            {
                { "glass", 25 }
            },
            OperatingCost = 10m
        };

        var response = await client.PostAsJsonAsync("/deliveries", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProcessDeliveryResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.GrossEarnings.ShouldBe(100m);
        body.NetEarnings.ShouldBe(90m);
    }

    [Fact]
    public async Task ProcessDelivery_WithMixedBottleTypes_CalculatesAllTypes()
    {
        var client = _fixture.Client;

        var request = new ProcessDeliveryRequest
        {
            TruckId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            LoadByType = new Dictionary<string, int>
            {
                { "glass", 10 },
                { "metal", 10 },
                { "plastic", 10 }
            },
            OperatingCost = 25m
        };

        var response = await client.PostAsJsonAsync("/deliveries", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProcessDeliveryResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.GrossEarnings.ShouldBe(82.5m);
        body.NetEarnings.ShouldBe(57.5m);
    }

    private record ProcessDeliveryRequest
    {
        public Guid TruckId { get; init; }
        public Guid PlayerId { get; init; }
        public Dictionary<string, int> LoadByType { get; init; } = new();
        public decimal OperatingCost { get; init; }
    }

    private record ProcessDeliveryResponse
    {
        public bool Success { get; init; }
        public Guid? DeliveryId { get; init; }
        public decimal GrossEarnings { get; init; }
        public decimal NetEarnings { get; init; }
        public string Message { get; init; } = string.Empty;
    }
}