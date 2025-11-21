using HeadquartersService.Models;

namespace HeadquartersService.Services;

public interface IFleetService
{
    void AddTruck(Truck t);
    IReadOnlyList<Truck> GetAll();
    IReadOnlyList<Truck> GetAvailableTrucks();
    bool TryAssignTruck(DispatchRequest req, out Truck? assigned);
    Truck? Get(Guid id);
}