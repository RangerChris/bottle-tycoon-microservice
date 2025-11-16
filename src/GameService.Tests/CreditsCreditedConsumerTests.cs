using GameService.Consumers;
using GameService.Events;
using GameService.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace GameService.Tests;

public class CreditsCreditedConsumerTests
{
    [Fact]
    public async Task Consume_ShouldCreditCredits_WhenPlayerExists()
    {
        // Arrange
        var playerServiceMock = new Mock<IPlayerService>();
        playerServiceMock.Setup(x => x.CreditCreditsAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var loggerMock = new Mock<ILogger<CreditsCreditedConsumer>>();
        var consumer = new CreditsCreditedConsumer(playerServiceMock.Object, loggerMock.Object);

        var contextMock = new Mock<ConsumeContext<CreditsCredited>>();
        var message = new CreditsCredited(Guid.NewGuid(), 100m, "test");
        contextMock.Setup(x => x.Message).Returns(message);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        playerServiceMock.Verify(x => x.CreditCreditsAsync(message.PlayerId, message.Amount, message.Reason), Times.Once);
        loggerMock.Verify(x => x.Log(LogLevel.Information, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task Consume_ShouldThrowException_WhenCreditFails()
    {
        // Arrange
        var playerServiceMock = new Mock<IPlayerService>();
        playerServiceMock.Setup(x => x.CreditCreditsAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        var loggerMock = new Mock<ILogger<CreditsCreditedConsumer>>();
        var consumer = new CreditsCreditedConsumer(playerServiceMock.Object, loggerMock.Object);

        var contextMock = new Mock<ConsumeContext<CreditsCredited>>();
        var message = new CreditsCredited(Guid.NewGuid(), 100m, "test");
        contextMock.Setup(x => x.Message).Returns(message);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => consumer.Consume(contextMock.Object));
        exception.Message.ShouldContain("Failed to credit credits");

        loggerMock.Verify(x => x.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }
}