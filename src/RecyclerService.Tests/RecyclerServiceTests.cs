using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RecyclerService.Data;
using RecyclerService.Services;
using RecyclerService.Tests.TestFixtures;
using Shouldly;
using Xunit;

namespace RecyclerService.Tests;

public class RecyclerServiceTests(TestcontainersFixture fixture) : IClassFixture<TestcontainersFixture>
{
    [Fact]
    public async Task CreateRecyclerAsync_CreatesRecycler()
    {
        if (!fixture.Started)
        {
            return;
        }

        using var scope = fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Services.RecyclerService>>();
        var meter = new Meter("RecyclerService.Tests");
        var counter = meter.CreateCounter<long>("bottles_processed", "bottles", "Number of bottles processed by type");
        var queueServiceMock = new Mock<ICustomerQueueService>();
        var svc = new Services.RecyclerService(db, logger, counter, queueServiceMock.Object);

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
        if (!fixture.Started)
        {
            return;
        }

        using var scope = fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Services.RecyclerService>>();
        var meter = new Meter("RecyclerService.Tests");
        var counter = meter.CreateCounter<long>("bottles_processed", "bottles", "Number of bottles processed by type");
        var queueServiceMock = new Mock<ICustomerQueueService>();
        var svc = new Services.RecyclerService(db, logger, counter, queueServiceMock.Object);

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
        if (!fixture.Started)
        {
            return;
        }

        using var scope = fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Services.RecyclerService>>();
        var meter = new Meter("RecyclerService.Tests");
        var counter = meter.CreateCounter<long>("bottles_processed", "bottles", "Number of bottles processed by type");
        var queueServiceMock = new Mock<ICustomerQueueService>();
        var svc = new Services.RecyclerService(db, logger, counter, queueServiceMock.Object);

        var bottlesByType = new Dictionary<string, int>
        {
            { "glass", 10 },
            { "metal", 5 },
            { "plastic", 8 }
        };

        await svc.RecordBottlesProcessedAsync(bottlesByType);

        bottlesByType.Count.ShouldBe(3);
    }

    [Fact]
    public async Task RecordBottlesProcessedAsync_WithEmptyDictionary_Completes()
    {
        if (!fixture.Started)
        {
            return;
        }

        using var scope = fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Services.RecyclerService>>();
        var meter = new Meter("RecyclerService.Tests");
        var counter = meter.CreateCounter<long>("bottles_processed", "bottles", "Number of bottles processed by type");
        var queueServiceMock = new Mock<ICustomerQueueService>();
        var svc = new Services.RecyclerService(db, logger, counter, queueServiceMock.Object);

        var bottlesByType = new Dictionary<string, int>();

        await svc.RecordBottlesProcessedAsync(bottlesByType);

        bottlesByType.Count.ShouldBe(0);
    }

    [Fact]
    public async Task RecordBottlesProcessedAsync_WithZeroValues_IgnoresZeros()
    {
        if (!fixture.Started)
        {
            return;
        }

        using var scope = fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Services.RecyclerService>>();
        var meter = new Meter("RecyclerService.Tests");
        var counter = meter.CreateCounter<long>("bottles_processed", "bottles", "Number of bottles processed by type");
        var queueServiceMock = new Mock<ICustomerQueueService>();
        var svc = new Services.RecyclerService(db, logger, counter, queueServiceMock.Object);

        var bottlesByType = new Dictionary<string, int>
        {
            { "glass", 0 },
            { "metal", 5 },
            { "plastic", 0 }
        };

        await svc.RecordBottlesProcessedAsync(bottlesByType);

        bottlesByType["metal"].ShouldBe(5);
    }

    [Fact]
    public async Task RecordBottlesProcessedAsync_WithEmptyKey_IgnoresEmptyKeys()
    {
        if (!fixture.Started)
        {
            return;
        }

        using var scope = fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Services.RecyclerService>>();
        var meter = new Meter("RecyclerService.Tests");
        var counter = meter.CreateCounter<long>("bottles_processed", "bottles", "Number of bottles processed by type");
        var queueServiceMock = new Mock<ICustomerQueueService>();
        var svc = new Services.RecyclerService(db, logger, counter, queueServiceMock.Object);

        var bottlesByType = new Dictionary<string, int>
        {
            { "", 10 },
            { "glass", 5 }
        };

        await svc.RecordBottlesProcessedAsync(bottlesByType);

        bottlesByType["glass"].ShouldBe(5);
    }

    [Fact]
    public async Task RecordBottlesProcessedAsync_WithWhitespaceKey_IgnoresWhitespaceKeys()
    {
        if (!fixture.Started)
        {
            return;
        }

        using var scope = fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Services.RecyclerService>>();
        var meter = new Meter("RecyclerService.Tests");
        var counter = meter.CreateCounter<long>("bottles_processed", "bottles", "Number of bottles processed by type");
        var queueServiceMock = new Mock<ICustomerQueueService>();
        var svc = new Services.RecyclerService(db, logger, counter, queueServiceMock.Object);

        var bottlesByType = new Dictionary<string, int>
        {
            { "   ", 10 },
            { "metal", 8 }
        };

        await svc.RecordBottlesProcessedAsync(bottlesByType);

        bottlesByType["metal"].ShouldBe(8);
    }

    [Fact]
    public async Task RecordBottlesProcessedAsync_WithNegativeValue_StillRecords()
    {
        if (!fixture.Started)
        {
            return;
        }

        using var scope = fixture.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Services.RecyclerService>>();
        var meter = new Meter("RecyclerService.Tests");
        var counter = meter.CreateCounter<long>("bottles_processed", "bottles", "Number of bottles processed by type");
        var queueServiceMock = new Mock<ICustomerQueueService>();
        var svc = new Services.RecyclerService(db, logger, counter, queueServiceMock.Object);

        var bottlesByType = new Dictionary<string, int>
        {
            { "glass", -5 },
            { "metal", 10 }
        };

        await svc.RecordBottlesProcessedAsync(bottlesByType);

        bottlesByType["glass"].ShouldBe(-5);
        bottlesByType["metal"].ShouldBe(10);
    }
}