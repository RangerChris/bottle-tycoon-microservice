using GameService.Data;
using GameService.Services;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace GameService.Tests;

public class PlayerServiceTests
{
    private readonly GameDbContext _context;
    private readonly PlayerService _service;

    public PlayerServiceTests()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GameDbContext(options);
        _service = new PlayerService(_context);
    }

    [Fact]
    public async Task CreatePlayerAsync_ShouldCreatePlayerWithStartingCredits()
    {
        // Act
        var player = await _service.CreatePlayerAsync();

        // Assert
        player.ShouldNotBeNull();
        player.Id.ShouldNotBe(Guid.Empty);
        player.Credits.ShouldBe(1000);
    }

    [Fact]
    public async Task GetPlayerAsync_ShouldReturnPlayer_WhenExists()
    {
        // Arrange
        var player = await _service.CreatePlayerAsync();

        // Act
        var retrieved = await _service.GetPlayerAsync(player.Id);

        // Assert
        retrieved.ShouldNotBeNull();
        retrieved!.Id.ShouldBe(player.Id);
    }

    [Fact]
    public async Task GetPlayerAsync_ShouldReturnNull_WhenNotExists()
    {
        // Act
        var player = await _service.GetPlayerAsync(Guid.NewGuid());

        // Assert
        player.ShouldBeNull();
    }

    [Fact]
    public async Task DebitCreditsAsync_ShouldSucceed_WhenSufficientCredits()
    {
        // Arrange
        var player = await _service.CreatePlayerAsync();

        // Act
        var success = await _service.DebitCreditsAsync(player.Id, 100, "Test");

        // Assert
        success.ShouldBeTrue();
        var updatedPlayer = await _service.GetPlayerAsync(player.Id);
        updatedPlayer!.Credits.ShouldBe(900);
    }

    [Fact]
    public async Task DebitCreditsAsync_ShouldFail_WhenInsufficientCredits()
    {
        // Arrange
        var player = await _service.CreatePlayerAsync();

        // Act
        var success = await _service.DebitCreditsAsync(player.Id, 2000, "Test");

        // Assert
        success.ShouldBeFalse();
        var updatedPlayer = await _service.GetPlayerAsync(player.Id);
        updatedPlayer!.Credits.ShouldBe(1000);
    }

    [Fact]
    public async Task CreditCreditsAsync_ShouldIncreaseCredits()
    {
        // Arrange
        var player = await _service.CreatePlayerAsync();

        // Act
        var success = await _service.CreditCreditsAsync(player.Id, 500, "Test");

        // Assert
        success.ShouldBeTrue();
        var updatedPlayer = await _service.GetPlayerAsync(player.Id);
        updatedPlayer!.Credits.ShouldBe(1500);
    }
}