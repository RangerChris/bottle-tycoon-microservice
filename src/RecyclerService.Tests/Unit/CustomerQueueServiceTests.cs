using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RecyclerService.Data;
using RecyclerService.Models;
using RecyclerService.Services;
using Shouldly;
using Xunit;

namespace RecyclerService.Tests.Unit;

public class CustomerQueueServiceTests
{
    private static RecyclerDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<RecyclerDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new RecyclerDbContext(options);
    }

    [Fact]
    public async Task GetNextWaitingCustomerAsync_ReturnsOldestWaitingAndMarksProcessing()
    {
        var ct = TestContext.Current.CancellationToken;
        var dbName = Guid.NewGuid().ToString();
        await using var db = CreateContext(dbName);

        var recyclerId = Guid.NewGuid();

        var older = new Customer
        {
            Id = Guid.NewGuid(),
            RecyclerId = recyclerId,
            ArrivedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            Status = CustomerStatus.Waiting
        };

        var newer = new Customer
        {
            Id = Guid.NewGuid(),
            RecyclerId = recyclerId,
            ArrivedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            Status = CustomerStatus.Waiting
        };

        // other recycler customer should be ignored
        var other = new Customer
        {
            Id = Guid.NewGuid(),
            RecyclerId = Guid.NewGuid(),
            ArrivedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            Status = CustomerStatus.Waiting
        };

        await db.Customers.AddRangeAsync([older, newer, other], ct);
        await db.SaveChangesAsync(ct);

        var loggerMock = new Mock<ILogger<CustomerQueueService>>();
        var svc = new CustomerQueueService(db, loggerMock.Object);

        var next = await svc.GetNextWaitingCustomerAsync(recyclerId, ct);

        next.ShouldNotBeNull();
        next.Id.ShouldBe(older.Id); // oldest waiting
        next.Status.ShouldBe(CustomerStatus.Processing);
        next.ServiceStartedAt.ShouldNotBeNull();

        // verify persisted
        var persistedCustomer = await db.Customers.FirstOrDefaultAsync(c => c.Id == next.Id, ct);
        persistedCustomer.ShouldNotBeNull();
        persistedCustomer.Status.ShouldBe(CustomerStatus.Processing);
        persistedCustomer.ServiceStartedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetNextWaitingCustomerAsync_WhenNoWaiting_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var dbName = Guid.NewGuid().ToString();
        await using var db = CreateContext(dbName);

        var recyclerId = Guid.NewGuid();

        var c = new Customer
        {
            Id = Guid.NewGuid(),
            RecyclerId = recyclerId,
            ArrivedAt = DateTimeOffset.UtcNow,
            Status = CustomerStatus.Processing
        };

        await db.Customers.AddAsync(c, ct);
        await db.SaveChangesAsync(ct);

        var loggerMock = new Mock<ILogger<CustomerQueueService>>();
        var svc = new CustomerQueueService(db, loggerMock.Object);

        var next = await svc.GetNextWaitingCustomerAsync(recyclerId, ct);

        next.ShouldBeNull();
    }

    [Fact]
    public async Task MarkAsDoneAsync_SetsDoneAndProcessedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var dbName = Guid.NewGuid().ToString();
        await using var db = CreateContext(dbName);

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            RecyclerId = Guid.NewGuid(),
            ArrivedAt = DateTimeOffset.UtcNow,
            Status = CustomerStatus.Processing
        };

        await db.Customers.AddAsync(customer, ct);
        await db.SaveChangesAsync(ct);

        var loggerMock = new Mock<ILogger<CustomerQueueService>>();
        var svc = new CustomerQueueService(db, loggerMock.Object);

        await svc.MarkAsDoneAsync(customer.Id, ct);

        var persistedCustomer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customer.Id, ct);
        persistedCustomer.ShouldNotBeNull();
        persistedCustomer.Status.ShouldBe(CustomerStatus.Done);
        persistedCustomer.ProcessedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetQueueDepthAsync_ReturnsCountOfWaiting()
    {
        var ct = TestContext.Current.CancellationToken;
        var dbName = Guid.NewGuid().ToString();
        await using var db = CreateContext(dbName);

        var recyclerId = Guid.NewGuid();

        var waiting1 = new Customer { Id = Guid.NewGuid(), RecyclerId = recyclerId, Status = CustomerStatus.Waiting };
        var waiting2 = new Customer { Id = Guid.NewGuid(), RecyclerId = recyclerId, Status = CustomerStatus.Waiting };
        var processing = new Customer { Id = Guid.NewGuid(), RecyclerId = recyclerId, Status = CustomerStatus.Processing };
        var otherRecycler = new Customer { Id = Guid.NewGuid(), RecyclerId = Guid.NewGuid(), Status = CustomerStatus.Waiting };

        await db.Customers.AddRangeAsync([waiting1, waiting2, processing, otherRecycler], ct);
        await db.SaveChangesAsync(ct);

        var loggerMock = new Mock<ILogger<CustomerQueueService>>();
        var svc = new CustomerQueueService(db, loggerMock.Object);

        var depth = await svc.GetQueueDepthAsync(recyclerId, ct);

        depth.ShouldBe(2);
    }
}