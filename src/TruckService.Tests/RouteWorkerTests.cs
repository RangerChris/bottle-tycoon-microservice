using System.Data.Common;
using MassTransit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using TruckService.Data;
using TruckService.Services;
using Xunit;

namespace TruckService.Tests;

public class RouteWorkerTests
{
    private TruckDbContext CreateInMemoryDb(out DbConnection connection)
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<TruckDbContext>().UseSqlite((SqliteConnection)connection).Options;
        var db = new TruckDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task RouteWorker_ProcessesQueuedDelivery_AndUpdatesTruck()
    {
        var db = CreateInMemoryDb(out var conn);

        // seed truck
        var truck = new TruckEntity { Id = Guid.NewGuid(), LicensePlate = "T-100", Model = "M", IsActive = true, CapacityLevel = 0 };
        db.Trucks.Add(truck);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // add a queued delivery
        var delivery = new DeliveryEntity
        {
            Id = Guid.NewGuid(),
            TruckId = truck.Id,
            RecyclerId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            State = "Queued",
            LoadByTypeJson = DeliveryEntity.SerializeLoad(new Dictionary<string, int> { { "glass", 10 } }),
            GrossEarnings = 40m,
            OperatingCost = 5m,
            NetProfit = 35m
        };
        db.Deliveries.Add(delivery);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var logger = new Mock<ILogger<RouteWorker>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var worker = new RouteWorker(db, logger.Object, publishEndpoint.Object);

        await worker.RunOnceAsync(TestContext.Current.CancellationToken);

        var processed = await db.Deliveries.FindAsync(new object[] { delivery.Id }, TestContext.Current.CancellationToken);
        processed.ShouldNotBeNull();
        processed.State.ShouldBe("Completed");
        processed.CompletedAt.ShouldNotBeNull();

        var truckAfter = await db.Trucks.FindAsync(new object[] { truck.Id }, TestContext.Current.CancellationToken);
        truckAfter?.TotalEarnings.ShouldBe(35m);
        truckAfter?.GetCurrentLoadByType().ShouldBeEmpty();

        await conn.CloseAsync();
    }
}