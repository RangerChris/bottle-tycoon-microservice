using HeadquartersService.Models;

namespace HeadquartersService.Services;

public class FleetService : IFleetService
{
    private readonly List<Truck> _trucks = new();

    public void AddTruck(Truck t)
    {
        _trucks.Add(t);
    }

    public IReadOnlyList<Truck> GetAll()
    {
        return _trucks.ToList();
    }

    public IReadOnlyList<Truck> GetAvailableTrucks()
    {
        return _trucks.Where(t => t.Status == TruckStatus.Idle && t.CurrentLoad == 0).ToList();
    }

    public bool TryAssignTruck(DispatchRequest req, out Truck? assigned)
    {
        assigned = null;
        var candidates = GetAvailableTrucks()
            .Where(t => t.Capacity >= req.ExpectedBottles * 0.95) // buffer: allow slightly smaller expected fill
            .ToList();

        if (!candidates.Any())
        {
            return false;
        }

        // prefer smallest capacity that fits
        var best = candidates.OrderBy(t => t.Capacity)
            .ThenByDescending(t => t.Reliability)
            .ThenBy(t => t.TotalDistance)
            .First();

        best.Status = TruckStatus.Assigned;
        assigned = best;
        req.AssignedTruckId = best.Id;
        req.Status = DispatchStatus.Assigned;
        return true;
    }

    public Truck? Get(Guid id)
    {
        return _trucks.FirstOrDefault(t => t.Id == id);
    }

    public void Reset()
    {
        _trucks.Clear();
    }
}