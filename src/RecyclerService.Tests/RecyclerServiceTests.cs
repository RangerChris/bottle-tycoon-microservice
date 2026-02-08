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
        var svc = new Services.RecyclerService(db, logger, meter);

        // Clean up any existing data
        await db.Recyclers.ExecuteDeleteAsync(Xunit.TestContext.Current.CancellationToken);

        var recycler = await svc.CreateRecyclerAsync();
        recycler.ShouldNotBeNull();
        recycler.Id.ShouldNotBe(Guid.Empty);
        recycler.Capacity.ShouldBe(100); // default
        recycler.CreatedAt.ShouldNotBe(default);

        var fetched = await svc.GetByIdAsync(recycler.Id, TestContext.Current.CancellationToken);
        fetched.ShouldNotBeNull();
        fetched.Id.ShouldBe(recycler.Id);
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
        var svc = new Services.RecyclerService(db, logger, meter);

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
}