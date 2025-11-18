// filepath: d:\projects\bottle-tycoon-microservice\src\GameService.Tests\DeliveryCompletedConsumerTests.cs

using GameService.Consumers;
using GameService.Events;
using GameService.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace GameService.Tests;

public class DeliveryCompletedConsumerTests
{
    [Fact]
    public async Task Consume_ShouldCreditPlayer_WhenPlayerIdPresent()
    {
        var playerServiceMock = new Mock<IPlayerService>();
        playerServiceMock.Setup(x => x.CreditCreditsAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var loggerMock = new Mock<ILogger<DeliveryCompletedConsumer>>();
        var consumer = new DeliveryCompletedConsumer(playerServiceMock.Object, loggerMock.Object);

        var contextMock = new Mock<ConsumeContext<DeliveryCompleted>>();
        var message = new DeliveryCompleted(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, new Dictionary<string, int> { { "glass", 10 } }, 123.45m);
        contextMock.Setup(x => x.Message).Returns(message);

        await consumer.Consume(contextMock.Object);

        playerServiceMock.Verify(x => x.CreditCreditsAsync(message.PlayerId, message.CreditsEarned, "DeliveryCompleted"), Times.Once);
        loggerMock.Verify(x => x.Log(LogLevel.Information, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task Consume_ShouldSkip_WhenPlayerIdEmpty()
    {
        var playerServiceMock = new Mock<IPlayerService>();
        var loggerMock = new Mock<ILogger<DeliveryCompletedConsumer>>();
        var consumer = new DeliveryCompletedConsumer(playerServiceMock.Object, loggerMock.Object);

        var contextMock = new Mock<ConsumeContext<DeliveryCompleted>>();
        var message = new DeliveryCompleted(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, DateTimeOffset.UtcNow, new Dictionary<string, int> { { "plastic", 5 } }, 10m);
        contextMock.Setup(x => x.Message).Returns(message);

        await consumer.Consume(contextMock.Object);

        playerServiceMock.Verify(x => x.CreditCreditsAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>()), Times.Never);
        loggerMock.Verify(x => x.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task Consume_ShouldThrow_WhenCreditingFails()
    {
        var playerServiceMock = new Mock<IPlayerService>();
        playerServiceMock.Setup(x => x.CreditCreditsAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        var loggerMock = new Mock<ILogger<DeliveryCompletedConsumer>>();
        var consumer = new DeliveryCompletedConsumer(playerServiceMock.Object, loggerMock.Object);

        var contextMock = new Mock<ConsumeContext<DeliveryCompleted>>();
        var message = new DeliveryCompleted(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, new Dictionary<string, int> { { "metal", 2 } }, 5m);
        contextMock.Setup(x => x.Message).Returns(message);

        var ex = await Assert.ThrowsAsync<Exception>(() => consumer.Consume(contextMock.Object));
        ex.Message.ShouldContain("Failed to credit player");
        loggerMock.Verify(x => x.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }
}