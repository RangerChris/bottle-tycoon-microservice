using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RecyclerService.Data;
using RecyclerService.Events;
using RecyclerService.Models;
using Shouldly;
using Xunit;

namespace RecyclerService.Tests;

public class RecyclerServiceTests
{
    private RecyclerDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<RecyclerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new RecyclerDbContext(options);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnRecycler_WhenExists()
    {
        var db = CreateInMemoryDb();
        var recycler = new Recycler { Id = Guid.NewGuid(), Name = "Test Recycler", Capacity = 100, CurrentLoad = 0 };
        db.Recyclers.Add(recycler);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new Services.RecyclerService(db, Mock.Of<IPublishEndpoint>(), Mock.Of<ILogger<Services.RecyclerService>>());

        var result = await service.GetByIdAsync(recycler.Id, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(recycler.Id);
        result.Name.ShouldBe(recycler.Name);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        var db = CreateInMemoryDb();
        var service = new Services.RecyclerService(db, Mock.Of<IPublishEndpoint>(), Mock.Of<ILogger<Services.RecyclerService>>());

        var result = await service.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task VisitorArrivedAsync_ShouldAddVisitorAndIncreaseLoad()
    {
        var db = CreateInMemoryDb();
        var recycler = new Recycler { Id = Guid.NewGuid(), Name = "Test Recycler", Capacity = 100, CurrentLoad = 10 };
        db.Recyclers.Add(recycler);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var publisherMock = new Mock<IPublishEndpoint>();
        var service = new Services.RecyclerService(db, publisherMock.Object, Mock.Of<ILogger<Services.RecyclerService>>());

        var visitor = new Visitor { Bottles = 25, VisitorType = "Regular" };

        var result = await service.VisitorArrivedAsync(recycler.Id, visitor, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(recycler.Id);
        result.CurrentLoad.ShouldBe(35); // 10 + 25

        var savedVisitor = await db.Visitors.FirstOrDefaultAsync(v => v.RecyclerId == recycler.Id, TestContext.Current.CancellationToken);
        savedVisitor.ShouldNotBeNull();
        savedVisitor.Bottles.ShouldBe(25);
        savedVisitor.VisitorType.ShouldBe("Regular");

        // Should not publish event since not full (35 < 100)
        publisherMock.Verify(p => p.Publish(It.IsAny<RecyclerFull>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task VisitorArrivedAsync_ShouldPublishEvent_WhenRecyclerBecomesFull()
    {
        var db = CreateInMemoryDb();
        var recycler = new Recycler { Id = Guid.NewGuid(), Name = "Test Recycler", Capacity = 100, CurrentLoad = 80 };
        db.Recyclers.Add(recycler);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var publisherMock = new Mock<IPublishEndpoint>();
        var service = new Services.RecyclerService(db, publisherMock.Object, Mock.Of<ILogger<Services.RecyclerService>>());

        var visitor = new Visitor { Bottles = 25, VisitorType = "Regular" };

        var result = await service.VisitorArrivedAsync(recycler.Id, visitor, TestContext.Current.CancellationToken);

        result.CurrentLoad.ShouldBe(105); // 80 + 25

        // Should publish RecyclerFull event
        publisherMock.Verify(p => p.Publish(It.Is<RecyclerFull>(e =>
            e.RecyclerId == recycler.Id &&
            e.Capacity == 100 &&
            e.CurrentLoad == 105), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VisitorArrivedAsync_ShouldThrow_WhenRecyclerNotFound()
    {
        var db = CreateInMemoryDb();
        var publisherMock = new Mock<IPublishEndpoint>();
        var service = new Services.RecyclerService(db, publisherMock.Object, Mock.Of<ILogger<Services.RecyclerService>>());

        var visitor = new Visitor { Bottles = 10, VisitorType = "Regular" };

        await Should.ThrowAsync<KeyNotFoundException>(() =>
            service.VisitorArrivedAsync(Guid.NewGuid(), visitor));
    }

    [Fact]
    public async Task VisitorArrivedAsync_ShouldSetVisitorIdAndArrivedAt_WhenNotProvided()
    {
        var db = CreateInMemoryDb();
        var recycler = new Recycler { Id = Guid.NewGuid(), Name = "Test Recycler", Capacity = 100, CurrentLoad = 0 };
        db.Recyclers.Add(recycler);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var publisherMock = new Mock<IPublishEndpoint>();
        var service = new Services.RecyclerService(db, publisherMock.Object, Mock.Of<ILogger<Services.RecyclerService>>());

        var visitor = new Visitor { Bottles = 15, VisitorType = "Premium" };

        await service.VisitorArrivedAsync(recycler.Id, visitor, TestContext.Current.CancellationToken);

        var savedVisitor = await db.Visitors.FirstOrDefaultAsync(v => v.RecyclerId == recycler.Id, TestContext.Current.CancellationToken);
        savedVisitor.ShouldNotBeNull();
        savedVisitor.Id.ShouldNotBe(Guid.Empty);
        savedVisitor.ArrivedAt.ShouldNotBe(default);
        savedVisitor.RecyclerId.ShouldBe(recycler.Id);
    }

    [Fact]
    public async Task VisitorArrivedAsync_ShouldUseProvidedVisitorIdAndArrivedAt()
    {
        var db = CreateInMemoryDb();
        var recycler = new Recycler { Id = Guid.NewGuid(), Name = "Test Recycler", Capacity = 100, CurrentLoad = 0 };
        db.Recyclers.Add(recycler);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var publisherMock = new Mock<IPublishEndpoint>();
        var service = new Services.RecyclerService(db, publisherMock.Object, Mock.Of<ILogger<Services.RecyclerService>>());

        var customId = Guid.NewGuid();
        var customTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        var visitor = new Visitor { Id = customId, Bottles = 20, VisitorType = "Regular", ArrivedAt = customTime };

        await service.VisitorArrivedAsync(recycler.Id, visitor, TestContext.Current.CancellationToken);

        var savedVisitor = await db.Visitors.FirstOrDefaultAsync(v => v.Id == customId, TestContext.Current.CancellationToken);
        savedVisitor.ShouldNotBeNull();
        savedVisitor.Id.ShouldBe(customId);
        savedVisitor.ArrivedAt.ShouldBe(customTime);
    }

    [Fact]
    public async Task VisitorArrivedAsync_ShouldLogInformation()
    {
        var db = CreateInMemoryDb();
        var recycler = new Recycler { Id = Guid.NewGuid(), Name = "Test Recycler", Capacity = 100, CurrentLoad = 10 };
        db.Recyclers.Add(recycler);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var publisherMock = new Mock<IPublishEndpoint>();
        var loggerMock = new Mock<ILogger<Services.RecyclerService>>();
        var service = new Services.RecyclerService(db, publisherMock.Object, loggerMock.Object);

        var visitor = new Visitor { Id = Guid.NewGuid(), Bottles = 25, VisitorType = "Regular", ArrivedAt = DateTimeOffset.UtcNow };

        await service.VisitorArrivedAsync(recycler.Id, visitor, TestContext.Current.CancellationToken);

        loggerMock.Verify(l => l.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Visitor") && o.ToString()!.Contains("arrived")),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task VisitorArrivedAsync_ShouldLogWhenFull()
    {
        var db = CreateInMemoryDb();
        var recycler = new Recycler { Id = Guid.NewGuid(), Name = "Test Recycler", Capacity = 100, CurrentLoad = 90 };
        db.Recyclers.Add(recycler);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var publisherMock = new Mock<IPublishEndpoint>();
        var loggerMock = new Mock<ILogger<Services.RecyclerService>>();
        var service = new Services.RecyclerService(db, publisherMock.Object, loggerMock.Object);

        var visitor = new Visitor { Bottles = 15, VisitorType = "Regular" };

        await service.VisitorArrivedAsync(recycler.Id, visitor, TestContext.Current.CancellationToken);

        loggerMock.Verify(l => l.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("reached capacity")),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }
}