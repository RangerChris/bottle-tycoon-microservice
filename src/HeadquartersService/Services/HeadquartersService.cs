using HeadquartersService.Models;

namespace HeadquartersService.Services;

public class HeadquartersService : IHeadquartersService
{
    private readonly IDispatchQueue _dispatchQueue;
    private readonly IFleetService _fleetService;

    public HeadquartersService(IFleetService fleetService, IDispatchQueue dispatchQueue)
    {
        _fleetService = fleetService;
        _dispatchQueue = dispatchQueue;
    }

    public Task ResetAsync()
    {
        _fleetService.Reset();
        _dispatchQueue.Reset();
        return Task.CompletedTask;
    }

    public Task InitializeFleetAsync()
    {
        // Add a default truck to the fleet
        var truck = new Truck
        {
            Id = Guid.NewGuid(),
            Capacity = 50,
            Status = TruckStatus.Idle,
            Reliability = 0.95
        };

        _fleetService.AddTruck(truck);

        return Task.CompletedTask;
    }
}