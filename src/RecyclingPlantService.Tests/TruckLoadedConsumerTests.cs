using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using RecyclingPlantService.Consumers;
using RecyclingPlantService.Services;
using Shared.Events;
using Xunit;

namespace RecyclingPlantService.Tests;

public class TruckLoadedConsumerTests
{
    private readonly TruckLoadedConsumer _consumer;
    private readonly Mock<ILogger<TruckLoadedConsumer>> _loggerMock;
    private readonly Mock<IPublishEndpoint> _publishMock;
    private readonly Mock<IRecyclingPlantService> _serviceMock;

    public TruckLoadedConsumerTests()
    {
        _loggerMock = new Mock<ILogger<TruckLoadedConsumer>>();
        _serviceMock = new Mock<IRecyclingPlantService>();
        _publishMock = new Mock<IPublishEndpoint>();
        _consumer = new TruckLoadedConsumer(_loggerMock.Object, _serviceMock.Object, _publishMock.Object);
    }

    [Fact]
    public async Task Consume_ShouldCalculateCreditsAndPublishDeliveryCompleted()
    {
        // Arrange
        var truckId = Guid.NewGuid();
        var recyclerId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var loadByType = new Dictionary<string, int> { { "glass", 10 }, { "plastic", 5 } };
        var operatingCost = 2.5m;
        var loadedAt = DateTimeOffset.UtcNow;
        var message = new TruckLoaded(truckId, recyclerId, playerId, loadByType, operatingCost, loadedAt);
        var expectedGross = 10 * 4.0m + 5 * 1.75m; // 40 + 8.75 = 48.75
        var expectedNet = expectedGross - operatingCost; // 48.75 - 2.5 = 46.25

        var contextMock = new Mock<ConsumeContext<TruckLoaded>>();
        contextMock.Setup(c => c.Message).Returns(message);
        _serviceMock.Setup(s => s.CalculateEarnings(loadByType, operatingCost)).Returns((expectedGross, expectedNet));
        _serviceMock.Setup(s => s.ProcessDeliveryAsync(truckId, playerId, loadByType, operatingCost, loadedAt)).ReturnsAsync(Guid.NewGuid());

        // Act
        await _consumer.Consume(contextMock.Object);

        // Assert
        _publishMock.Verify(p => p.Publish(It.Is<EarningsCalculated>(ec =>
            ec.PlayerId == playerId &&
            ec.GrossEarnings == expectedGross &&
            ec.OperatingCost == operatingCost &&
            ec.NetEarnings == expectedNet), default));
        _publishMock.Verify(p => p.Publish(It.Is<EarningsPublished>(ep =>
            ep.PlayerId == playerId &&
            ep.NetEarnings == expectedNet), default));
    }
}