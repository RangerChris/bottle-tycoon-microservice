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

public class TruckManagerExtraTests
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
    public async Task GetFleetSummary_ReturnsTrucks()
    {
        var repoMock = new Mock<ITruckRepository>();
        var db = CreateInMemoryDb(out var conn);
        // seed a truck in db
        var truck = new TruckEntity { Id = Guid.NewGuid(), LicensePlate = "A", Model = "M", IsActive = true, CapacityLevel = 0 };
        db.Trucks.Add(truck);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var loadProvider = new TestLoadProvider(1, 1, 1);
        var logger = new Mock<ILogger<TruckManager>>();
        var manager = new TruckManager(repoMock.Object, db, loadProvider, logger.Object);

        var summary = await manager.GetFleetSummaryAsync(TestContext.Current.CancellationToken);
        summary.ShouldNotBeNull();
        summary.ShouldNotBeEmpty();

        conn.Close();
    }

    [Fact]
    public async Task GetStatus_ThrowsWhenNotFound()
    {
        var repoMock = new Mock<ITruckRepository>();
        var db = CreateInMemoryDb(out var conn);
        var loadProvider = new TestLoadProvider(1, 1, 1);
        var logger = new Mock<ILogger<TruckManager>>();
        var manager = new TruckManager(repoMock.Object, db, loadProvider, logger.Object);

        await Should.ThrowAsync<KeyNotFoundException>(async () => await manager.GetStatusAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));

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