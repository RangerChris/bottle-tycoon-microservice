using HeadquartersService.Models;
using HeadquartersService.Services;
using Moq;
using Xunit;

namespace HeadquartersService.Tests;

public class HeadquartersServiceTests
{
    private readonly Mock<IDispatchQueue> _dispatchQueueMock;
    private readonly Mock<IFleetService> _fleetServiceMock;
    private readonly Services.HeadquartersService _headquartersService;

    public HeadquartersServiceTests()
    {
        _fleetServiceMock = new Mock<IFleetService>();
        _dispatchQueueMock = new Mock<IDispatchQueue>();
        _headquartersService = new Services.HeadquartersService(_fleetServiceMock.Object, _dispatchQueueMock.Object);
    }

    [Fact]
    public async Task ResetAsync_ShouldResetFleetAndDispatchQueue()
    {
        // Act
        await _headquartersService.ResetAsync();

        // Assert
        _fleetServiceMock.Verify(f => f.Reset(), Times.Once);
        _dispatchQueueMock.Verify(d => d.Reset(), Times.Once);
    }

    [Fact]
    public async Task InitializeFleetAsync_ShouldAddDefaultTruck()
    {
        // Act
        await _headquartersService.InitializeFleetAsync();

        // Assert
        _fleetServiceMock.Verify(f => f.AddTruck(It.Is<Truck>(t =>
            t.Capacity == 50 &&
            t.Status == TruckStatus.Idle &&
            Math.Abs(t.Reliability - 0.95) < 0.0001 &&
            t.Id != Guid.Empty
        )), Times.Once);
    }
}