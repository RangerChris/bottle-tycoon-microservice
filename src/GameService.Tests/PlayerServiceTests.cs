using GameService.Data;
using GameService.Services;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace GameService.Tests;

public class PlayerServiceTests
{
    private GameDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new GameDbContext(options);
    }

    [Fact]
    public async Task CreatePlayer_ShouldAddPlayer()
    {
        var db = CreateInMemoryDb();
        var svc = new PlayerService(db);

        var player = await svc.CreatePlayerAsync();

        player.ShouldNotBeNull();
        (await db.Players.FindAsync([player.Id], TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    [Fact]
    public async Task CreditAndDebit_ShouldUpdateBalance()
    {
        var db = CreateInMemoryDb();
        var svc = new PlayerService(db);

        var player = await svc.CreatePlayerAsync();

        var credited = await svc.CreditCreditsAsync(player.Id, 100m, "test");
        credited.ShouldBeTrue();

        var reloaded = await svc.GetPlayerAsync(player.Id);
        // Player starts with 1000 credits by default
        reloaded!.Credits.ShouldBe(1100m);

        var debited = await svc.DebitCreditsAsync(player.Id, 40m, "buy");
        debited.ShouldBeTrue();

        var after = await svc.GetPlayerAsync(player.Id);
        after!.Credits.ShouldBe(1060m);
    }

    [Fact]
    public async Task Debit_ShouldFailWhenInsufficientFunds()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var svc = new PlayerService(db);
        var player = await svc.CreatePlayerAsync();

        // Act
        var success = await svc.DebitCreditsAsync(player.Id, 2000m, "buy");

        // Assert
        success.ShouldBeFalse();
        var reloaded = await svc.GetPlayerAsync(player.Id);
        reloaded!.Credits.ShouldBe(1000m); // unchanged
    }

    [Fact]
    public async Task PurchaseItem_ShouldDebitCredits()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var svc = new PlayerService(db);
        var player = await svc.CreatePlayerAsync();

        // Act
        var success = await svc.PurchaseItemAsync(player.Id, "truck", 100m);

        // Assert
        success.ShouldBeTrue();
        var reloaded = await svc.GetPlayerAsync(player.Id);
        reloaded!.Credits.ShouldBe(900m);
    }

    [Fact]
    public async Task UpgradeItem_ShouldDebitCreditsAndRecordUpgrade()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var svc = new PlayerService(db);
        var player = await svc.CreatePlayerAsync();

        // Act
        var success = await svc.UpgradeItemAsync(player.Id, "truck", 1, 2, 200m);

        // Assert
        success.ShouldBeTrue();
        var reloaded = await svc.GetPlayerAsync(player.Id);
        reloaded!.Credits.ShouldBe(800m);
        reloaded.Upgrades.ShouldContain(u => u.ItemType == "truck" && u.ItemId == 1 && u.NewLevel == 2);
    }
}