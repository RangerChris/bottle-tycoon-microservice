namespace HeadquartersService.Models;

public enum TruckStatus
{
    Idle,
    Assigned,
    InProgress,
    Unavailable
}

public class Truck
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Capacity { get; set; }
    public TruckStatus Status { get; set; } = TruckStatus.Idle;
    public int CurrentLoad { get; set; } = 0;
    public double Reliability { get; set; } = 1.0; // 0..1, higher is more reliable
    public double TotalDistance { get; set; } = 0.0; // used as tie-breaker (lower is preferred)
}