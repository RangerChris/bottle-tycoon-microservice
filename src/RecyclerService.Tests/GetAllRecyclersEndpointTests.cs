using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RecyclerService.Data;
using RecyclerService.Models;
using Shouldly;
using Xunit;

namespace RecyclerService.Tests;

public class GetAllRecyclersEndpointTests
{
    private RecyclerDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<RecyclerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new RecyclerDbContext(options);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllRecyclers()
    {
        var db = CreateInMemoryDb();

        var r1 = new Recycler { Id = Guid.NewGuid(), Name = "R1", Capacity = 50 };
        r1.SetBottleInventory(new Dictionary<string, int>());
        var r2 = new Recycler { Id = Guid.NewGuid(), Name = "R2", Capacity = 100 };
        r2.SetBottleInventory(new Dictionary<string, int>());

        db.Recyclers.Add(r1);
        db.Recyclers.Add(r2);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new Services.RecyclerService(db, Mock.Of<ILogger<Services.RecyclerService>>());

        var result = await service.GetAllAsync(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.Any(r => r.Id == r1.Id).ShouldBeTrue();
        result.Any(r => r.Id == r2.Id).ShouldBeTrue();
    }
}