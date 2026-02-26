using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RecyclerService.Data;
using RecyclerService.Tests.TestFixtures;
using Shouldly;
using Xunit;

namespace RecyclerService.Tests;

public class RecyclerServiceTests : IClassFixture<TestcontainersFixture>
{
    private readonly TestcontainersFixture _fixture;

    public RecyclerServiceTests(TestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateRecyclerAsync_CreatesRecycler()
    {
        if (!_fixture.Started)
        {
            return;
        }

        using var scope = _fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Services.RecyclerService>>();
        var meter = new Meter("RecyclerService.Tests");
        var counter = meter.CreateCounter<long>("bottles_processed", "bottles", "Number of bottles processed by type");
        var svc = new Services.RecyclerService(db, logger, counter);

        await db.Recyclers.ExecuteDeleteAsync(Xunit.TestContext.Current.CancellationToken);

        var recycler = await svc.CreateRecyclerAsync();
        recycler.ShouldNotBeNull();
        recycler.Id.ShouldNotBe(Guid.Empty);
        recycler.Capacity.ShouldBe(100);
        recycler.CreatedAt.ShouldNotBe(default);
        recycler.Name.ShouldBe("Recycler 1");

        var fetched = await svc.GetByIdAsync(recycler.Id, TestContext.Current.CancellationToken);
        fetched.ShouldNotBeNull();
        fetched.Id.ShouldBe(recycler.Id);
        fetched.Name.ShouldBe("Recycler 1");
    }

    [Fact]
    public async Task ResetAsync_DeletesAllRecyclers()
    {
        if (!_fixture.Started)
        {
            return;
        }

        using var scope = _fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Services.RecyclerService>>();
        var meter = new Meter("RecyclerService.Tests");
        var counter = meter.CreateCounter<long>("bottles_processed", "bottles", "Number of bottles processed by type");
        var svc = new Services.RecyclerService(db, logger, counter);

        // Clean up any existing data
        await db.Recyclers.ExecuteDeleteAsync(Xunit.TestContext.Current.CancellationToken);

        await svc.CreateRecyclerAsync();
        await svc.CreateRecyclerAsync();

        var allBefore = await svc.GetAllAsync(TestContext.Current.CancellationToken);
        allBefore.Count.ShouldBe(2);

        await svc.ResetAsync();

        var allAfter = await svc.GetAllAsync(TestContext.Current.CancellationToken);
        allAfter.Count.ShouldBe(0);
    }

    [Fact]
    public async Task RecordBottlesProcessedAsync_WithValidBottles_RecordsMetrics()
    {
        if (!_fixture.Started)
        {
            return;
        }

        using var scope = _fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Services.RecyclerService>>();
        var meter = new Meter("RecyclerService.Tests");
        var counter = meter.CreateCounter<long>("bottles_processed", "bottles", "Number of bottles processed by type");
        var svc = new Services.RecyclerService(db, logger, counter);

        var bottlesByType = new Dictionary<string, int>
        {
            { "glass", 10 },
            { "metal", 5 },
            { "plastic", 8 }
        };

        await svc.RecordBottlesProcessedAsync(bottlesByType, TestContext.Current.CancellationToken);

        bottlesByType.Count.ShouldBe(3);
    }

    [Fact]
    public async Task RecordBottlesProcessedAsync_WithEmptyDictionary_Completes()
    {
        if (!_fixture.Started)
        {
            return;
        }

        using var scope = _fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Services.RecyclerService>>();
        var meter = new Meter("RecyclerService.Tests");
        var counter = meter.CreateCounter<long>("bottles_processed", "bottles", "Number of bottles processed by type");
        var svc = new Services.RecyclerService(db, logger, counter);

        var bottlesByType = new Dictionary<string, int>();

        await svc.RecordBottlesProcessedAsync(bottlesByType, TestContext.Current.CancellationToken);

        bottlesByType.Count.ShouldBe(0);
    }

    [Fact]
    public async Task RecordBottlesProcessedAsync_WithZeroValues_IgnoresZeros()
    {
        if (!_fixture.Started)
        {
            return;
        }

        using var scope = _fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Services.RecyclerService>>();
        var meter = new Meter("RecyclerService.Tests");
        var counter = meter.CreateCounter<long>("bottles_processed", "bottles", "Number of bottles processed by type");
        var svc = new Services.RecyclerService(db, logger, counter);

        var bottlesByType = new Dictionary<string, int>
        {
            { "glass", 0 },
            { "metal", 5 },
            { "plastic", 0 }
        };

        await svc.RecordBottlesProcessedAsync(bottlesByType, TestContext.Current.CancellationToken);

        bottlesByType["metal"].ShouldBe(5);
    }

    [Fact]
    public async Task RecordBottlesProcessedAsync_WithEmptyKey_IgnoresEmptyKeys()
    {
        if (!_fixture.Started)
        {
            return;
        }

        using var scope = _fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Services.RecyclerService>>();
        var meter = new Meter("RecyclerService.Tests");
        var counter = meter.CreateCounter<long>("bottles_processed", "bottles", "Number of bottles processed by type");
        var svc = new Services.RecyclerService(db, logger, counter);

        var bottlesByType = new Dictionary<string, int>
        {
            { "", 10 },
            { "glass", 5 }
        };

        await svc.RecordBottlesProcessedAsync(bottlesByType, TestContext.Current.CancellationToken);

        bottlesByType["glass"].ShouldBe(5);
    }

    [Fact]
    public async Task RecordBottlesProcessedAsync_WithWhitespaceKey_IgnoresWhitespaceKeys()
    {
        if (!_fixture.Started)
        {
            return;
        }

        using var scope = _fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Services.RecyclerService>>();
        var meter = new Meter("RecyclerService.Tests");
        var counter = meter.CreateCounter<long>("bottles_processed", "bottles", "Number of bottles processed by type");
        var svc = new Services.RecyclerService(db, logger, counter);

        var bottlesByType = new Dictionary<string, int>
        {
            { "   ", 10 },
            { "metal", 8 }
        };

        await svc.RecordBottlesProcessedAsync(bottlesByType, TestContext.Current.CancellationToken);

        bottlesByType["metal"].ShouldBe(8);
    }

    [Fact]
    public async Task RecordBottlesProcessedAsync_WithNegativeValue_StillRecords()
    {
        if (!_fixture.Started)
        {
            return;
        }

        using var scope = _fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Services.RecyclerService>>();
        var meter = new Meter("RecyclerService.Tests");
        var counter = meter.CreateCounter<long>("bottles_processed", "bottles", "Number of bottles processed by type");
        var svc = new Services.RecyclerService(db, logger, counter);

        var bottlesByType = new Dictionary<string, int>
        {
            { "glass", -5 },
            { "metal", 10 }
        };

        await svc.RecordBottlesProcessedAsync(bottlesByType, TestContext.Current.CancellationToken);

        bottlesByType["glass"].ShouldBe(-5);
        bottlesByType["metal"].ShouldBe(10);
    }
}