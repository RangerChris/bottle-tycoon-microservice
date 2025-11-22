using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using RecyclerService.Consumers;
using RecyclerService.Events;
using RecyclerService.Services;
using Shared.Events;
using Xunit;

namespace RecyclerService.Tests;

public class ConsumerTests
{
    [Fact]
    public async Task TruckArrivedConsumer_ConsumesSuccessfully()
    {
        var recyclerSvc = new Mock<IRecyclerService>();
        var logger = new Mock<ILogger<TruckArrivedConsumer>>();
        var consumer = new TruckArrivedConsumer(recyclerSvc.Object, logger.Object);

        var msg = new TruckArrived(TestContext.RandomGuid(), TestContext.RandomGuid(), DateTimeOffset.UtcNow);
        var ctx = new Mock<ConsumeContext<TruckArrived>>();
        ctx.SetupGet(x => x.Message).Returns(msg);

        await consumer.Consume(ctx.Object);

        // no exception, verify logger called
        logger.Verify(l => l.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => true),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task TruckLoadedConsumer_ConsumesSuccessfully()
    {
        var logger = new Mock<ILogger<TruckLoadedConsumer>>();
        var consumer = new TruckLoadedConsumer(logger.Object);

        var msg = new TruckLoaded(TestContext.RandomGuid(), TestContext.RandomGuid(), Guid.Empty, new Dictionary<string, int> { { "glass", 5 } }, 10m, DateTimeOffset.UtcNow);
        var ctx = new Mock<ConsumeContext<TruckLoaded>>();
        ctx.SetupGet(x => x.Message).Returns(msg);

        await consumer.Consume(ctx.Object);

        logger.Verify(l => l.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => true),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
    }
}