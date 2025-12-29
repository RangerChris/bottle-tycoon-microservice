using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RecyclingPlantService.Data;
using Shouldly;
using Xunit;

namespace RecyclingPlantService.Tests;

public class RecyclingPlantServiceTests : IDisposable
{
    private readonly RecyclingPlantDbContext _dbContext;
    private readonly Services.RecyclingPlantService _service;

    public RecyclingPlantServiceTests()
    {
        var options = new DbContextOptionsBuilder<RecyclingPlantDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new RecyclingPlantDbContext(options);
        var loggerMock = new Mock<ILogger<Services.RecyclingPlantService>>();
        _service = new Services.RecyclingPlantService(_dbContext, loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task ProcessDeliveryAsync_CalculatesEarningsAndCreatesDelivery()
    {
        // Arrange
        var truckId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var loadByType = new Dictionary<string, int>
        {
            ["glass"] = 2,
            ["metal"] = 3,
            ["plastic"] = 1
        };
        var operatingCost = 5.0m;
        var deliveredAt = DateTimeOffset.UtcNow;

        // Pre-existing earnings
        var existingPlayerEarnings = new PlayerEarnings { PlayerId = playerId, TotalEarnings = 10.0m, DeliveryCount = 2 };
        _dbContext.PlayerEarnings.Add(existingPlayerEarnings);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _service.ProcessDeliveryAsync(truckId, playerId, loadByType, operatingCost, deliveredAt);

        // Assert
        result.ShouldNotBe(Guid.Empty);

        var delivery = await _dbContext.PlantDeliveries.FindAsync([result], TestContext.Current.CancellationToken);
        delivery.ShouldNotBeNull();
        delivery.TruckId.ShouldBe(truckId);
        delivery.PlayerId.ShouldBe(playerId);
        delivery.GlassCount.ShouldBe(2);
        delivery.MetalCount.ShouldBe(3);
        delivery.PlasticCount.ShouldBe(1);
        delivery.GrossEarnings.ShouldBe(2 * 4.0m + 3 * 2.5m + 1 * 1.75m); // 8 + 7.5 + 1.75 = 17.25
        delivery.OperatingCost.ShouldBe(operatingCost);
        delivery.NetEarnings.ShouldBe(17.25m - operatingCost);
        delivery.DeliveredAt.ShouldBe(deliveredAt);

        // Verify PlayerEarnings update
        var updatedEarnings = await _dbContext.PlayerEarnings.FindAsync([playerId], TestContext.Current.CancellationToken);
        updatedEarnings.ShouldNotBeNull();
        updatedEarnings.TotalEarnings.ShouldBe(10.0m + (17.25m - operatingCost));
        updatedEarnings.DeliveryCount.ShouldBe(3);
        updatedEarnings.AverageEarnings.ShouldBe((10.0m + (17.25m - operatingCost)) / 3);
    }

    [Fact]
    public async Task ProcessDeliveryAsync_CreatesNewPlayerEarnings_WhenNotExists()
    {
        // Arrange
        var truckId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var loadByType = new Dictionary<string, int> { ["glass"] = 1 };
        var operatingCost = 1.0m;
        var deliveredAt = DateTimeOffset.UtcNow;

        // Act
        var result = await _service.ProcessDeliveryAsync(truckId, playerId, loadByType, operatingCost, deliveredAt);

        // Assert
        result.ShouldNotBe(Guid.Empty);

        var delivery = await _dbContext.PlantDeliveries.FindAsync([result], TestContext.Current.CancellationToken);
        delivery.ShouldNotBeNull();
        delivery.GrossEarnings.ShouldBe(4.0m);
        delivery.NetEarnings.ShouldBe(4.0m - operatingCost);

        var earnings = await _dbContext.PlayerEarnings.FindAsync([playerId], TestContext.Current.CancellationToken);
        earnings.ShouldNotBeNull();
        earnings.TotalEarnings.ShouldBe(4.0m - operatingCost);
        earnings.DeliveryCount.ShouldBe(1);
        earnings.AverageEarnings.ShouldBe(4.0m - operatingCost);
    }

    [Fact]
    public void CalculateEarnings_ReturnsCorrectValues()
    {
        // Arrange
        var loadByType = new Dictionary<string, int>
        {
            ["glass"] = 2,
            ["metal"] = 1,
            ["plastic"] = 3
        };
        var operatingCost = 2.0m;

        // Act
        var (gross, net) = _service.CalculateEarnings(loadByType, operatingCost);

        // Assert
        gross.ShouldBe(2 * 4.0m + 1 * 2.5m + 3 * 1.75m); // 8 + 2.5 + 5.25 = 15.75
        net.ShouldBe(15.75m - operatingCost);
    }
}