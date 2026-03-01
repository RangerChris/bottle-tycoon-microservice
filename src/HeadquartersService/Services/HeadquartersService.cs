using HeadquartersService.Models;

namespace HeadquartersService.Services;

public class HeadquartersService(IFleetService fleetService, IDispatchQueue dispatchQueue) : IHeadquartersService
{
    public Task ResetAsync()
    {
        fleetService.Reset();
        dispatchQueue.Reset();
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

        fleetService.AddTruck(truck);

        return Task.CompletedTask;
    }
}