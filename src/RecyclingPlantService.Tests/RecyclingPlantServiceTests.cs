using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RecyclingPlantService.Data;
using Xunit;

public class RecyclingPlantServiceTests
{
    private readonly RecyclingPlantDbContext _dbContext;
    private readonly Mock<ILogger<RecyclingPlantService.Services.RecyclingPlantService>> _loggerMock;
    private readonly RecyclingPlantService.Services.RecyclingPlantService _service;

    public RecyclingPlantServiceTests()
    {
        var options = new DbContextOptionsBuilder<RecyclingPlantDbContext>()
            .UseInMemoryDatabase("TestDb")
            .Options;
        _dbContext = new RecyclingPlantDbContext(options);
        _loggerMock = new Mock<ILogger<RecyclingPlantService.Services.RecyclingPlantService>>();
        _service = new RecyclingPlantService.Services.RecyclingPlantService(_dbContext, _loggerMock.Object);
    }

    [Fact]
    public void CalculateEarnings_ShouldSubtractOperatingCost()
    {
        // Arrange
        var loadByType = new Dictionary<string, int>
        {
            { "glass", 10 }
        };
        var operatingCost = 5.0m;

        // Act
        var (gross, net) = _service.CalculateEarnings(loadByType, operatingCost);

        // Assert
        Assert.Equal(40.0m, gross);
        Assert.Equal(35.0m, net);
    }

    [Fact]
    public void CalculateEarnings_ShouldIgnoreUnknownTypes()
    {
        // Arrange
        var loadByType = new Dictionary<string, int>
        {
            { "glass", 10 },
            { "unknown", 5 }
        };

        // Act
        var (gross, net) = _service.CalculateEarnings(loadByType, 0);

        // Assert
        Assert.Equal(40.0m, gross);
        Assert.Equal(40.0m, net);
    }

    [Fact]
    public void CalculateEarnings_ShouldReturnZeroForEmptyLoad()
    {
        // Arrange
        var loadByType = new Dictionary<string, int>();

        // Act
        var (gross, net) = _service.CalculateEarnings(loadByType, 0);

        // Assert
        Assert.Equal(0, gross);
        Assert.Equal(0, net);
    }
}