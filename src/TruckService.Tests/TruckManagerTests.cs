using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using TruckService.Data;
using TruckService.Models;
using TruckService.Services;
using Xunit;

namespace TruckService.Tests;

public class TruckManagerTests
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
    public async Task Dispatch_PersistsDelivery_AndCalculatesEarnings()
    {
        var repoMock = new Mock<ITruckRepository>();
        var truckId = Guid.NewGuid();
        repoMock.Setup(r => r.GetByIdAsync(truckId, It.IsAny<CancellationToken>())).ReturnsAsync(new TruckDto { Id = truckId, LicensePlate = "T" });

        var db = CreateInMemoryDb(out var conn);

        // deterministic load provider: returns large glass count to exercise trimming
        var loadProvider = new TestLoadProvider(200, 0, 0);

        var logger = new Mock<ILogger<TruckManager>>();
        var manager = new TruckManager(repoMock.Object, db, loadProvider, logger.Object);

        var ok = await manager.DispatchAsync(truckId, Guid.NewGuid(), 10, TestContext.Current.CancellationToken);
        ok.ShouldBeTrue();

        var deliveries = db.Deliveries.Where(d => d.TruckId == truckId).ToList();
        deliveries.Count.ShouldBe(1);
        deliveries[0].NetProfit.ShouldBeLessThanOrEqualTo(deliveries[0].GrossEarnings);

        conn.Close();
    }

    private class TestLoadProvider : ILoadProvider
    {
        private readonly int _g, _m, _p;

        public TestLoadProvider(int g, int m, int p)
        {
            _g = g;
            _m = m;
            _p = p;
        }

        public (int glass, int metal, int plastic) GetLoadForRecycler(Guid recyclerId)
        {
            return (_g, _m, _p);
        }
    }
}