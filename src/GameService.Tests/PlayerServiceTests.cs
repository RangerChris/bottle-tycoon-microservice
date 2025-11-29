using GameService.Data;
using GameService.Services;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace GameService.Tests;

public class PlayerServiceTests
{
    private static GameDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new GameDbContext(options);
    }

    [Fact]
    public async Task CreatePlayer_CreatesPlayer()
    {
        await using var db = CreateInMemoryDb();
        var svc = new PlayerService(db);

        var player = await svc.CreatePlayerAsync();
        player.ShouldNotBeNull();

        var fetched = await svc.GetPlayerAsync(player.Id);
        fetched.ShouldNotBeNull();
        fetched.Id.ShouldBe(player.Id);
    }

    [Fact]
    public async Task DebitCredits_Succeeds()
    {
        await using var db = CreateInMemoryDb();
        var svc = new PlayerService(db);
        var p = await svc.CreatePlayerAsync();
        var initial = p.Credits;

        var ok = await svc.DebitCreditsAsync(p.Id, 100m, "test");
        ok.ShouldBeTrue();

        var fetched = await svc.GetPlayerAsync(p.Id);
        fetched.ShouldNotBeNull();
        fetched.Credits.ShouldBe(initial - 100m);
    }

    [Fact]
    public async Task DebitCredits_Fails_WhenNotEnough()
    {
        await using var db = CreateInMemoryDb();
        var svc = new PlayerService(db);
        var p = await svc.CreatePlayerAsync();

        var ok = await svc.DebitCreditsAsync(p.Id, 99999m, "big");
        ok.ShouldBeFalse();
    }

    [Fact]
    public async Task CreditCredits_Works()
    {
        await using var db = CreateInMemoryDb();
        var svc = new PlayerService(db);
        var p = await svc.CreatePlayerAsync();
        var initial = p.Credits;

        var ok = await svc.CreditCreditsAsync(p.Id, 200m, "bonus");
        ok.ShouldBeTrue();

        var fetched = await svc.GetPlayerAsync(p.Id);
        fetched?.Credits.ShouldBe(initial + 200m);
    }


    [Fact]
    public async Task GetAllPlayers_ReturnsAll()
    {
        await using var db = CreateInMemoryDb();
        var svc = new PlayerService(db);

        var p1 = await svc.CreatePlayerAsync();
        var p2 = await svc.CreatePlayerAsync();

        var all = await svc.GetAllPlayersAsync();
        all.ShouldNotBeNull();
        all.Count.ShouldBe(2);
        all.ShouldContain(p => p.Id == p1.Id);
        all.ShouldContain(p => p.Id == p2.Id);
    }
}