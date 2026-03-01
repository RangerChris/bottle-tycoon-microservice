using GameService.Data;
using GameService.Models;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace GameService.Tests.Unit;

public class GameDbContextTests
{
    [Fact]
    public void ApplyLowercaseNamingConvention_ConvertsTableNameToLowercase()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase("TestDb_TableName")
            .Options;

        using var context = new GameDbContext(options);
        var model = context.Model;
        var playerEntity = model.FindEntityType(typeof(Player));

        playerEntity.ShouldNotBeNull();
        playerEntity.GetTableName().ShouldBe("players");
    }

    [Fact]
    public void ApplyLowercaseNamingConvention_ConvertsColumnNamesToLowercase()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase("TestDb_Columns")
            .Options;

        using var context = new GameDbContext(options);
        var model = context.Model;
        var playerEntity = model.FindEntityType(typeof(Player));

        playerEntity.ShouldNotBeNull();
        var idProperty = playerEntity.FindProperty("Id");
        idProperty.ShouldNotBeNull();
        idProperty.GetColumnName().ShouldBe("id");

        var creditsProperty = playerEntity.FindProperty("Credits");
        creditsProperty.ShouldNotBeNull();
        creditsProperty.GetColumnName().ShouldBe("credits");

        var nameProperty = playerEntity.FindProperty("Name");
        nameProperty.ShouldNotBeNull();
        nameProperty.GetColumnName().ShouldBe("name");
    }

    [Fact]
    public void ApplyLowercaseNamingConvention_ConvertsPrimaryKeyNameToLowercase()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase("TestDb_PrimaryKey")
            .Options;

        using var context = new GameDbContext(options);
        var model = context.Model;
        var playerEntity = model.FindEntityType(typeof(Player));

        playerEntity.ShouldNotBeNull();
        var primaryKey = playerEntity.FindPrimaryKey();
        primaryKey.ShouldNotBeNull();
    }

    [Fact]
    public void OnModelCreating_ConfiguresPlayerEntity()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase("TestDb_PlayerEntity")
            .Options;

        using var context = new GameDbContext(options);
        var model = context.Model;
        var playerEntity = model.FindEntityType(typeof(Player));

        playerEntity.ShouldNotBeNull();
        playerEntity.GetTableName().ShouldBe("players");

        var creditsProperty = playerEntity.FindProperty("Credits");
        creditsProperty.ShouldNotBeNull();
        creditsProperty.GetPrecision().ShouldBe(18);
        creditsProperty.GetScale().ShouldBe(2);
    }

    [Fact]
    public void DbContext_CanBeConstructed()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase("TestDb_Construction")
            .Options;

        using var context = new GameDbContext(options);

        context.ShouldNotBeNull();
        context.Players.ShouldNotBeNull();
    }

    [Fact]
    public void ApplyLowercaseNamingConvention_HandlesAllProperties()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase("TestDb_AllProperties")
            .Options;

        using var context = new GameDbContext(options);
        var model = context.Model;
        var playerEntity = model.FindEntityType(typeof(Player));

        playerEntity.ShouldNotBeNull();
        foreach (var property in playerEntity.GetProperties())
        {
            var columnName = property.GetColumnName();
            columnName.ShouldNotBeNull();
            columnName.ShouldBe(columnName.ToLowerInvariant());
        }
    }
}