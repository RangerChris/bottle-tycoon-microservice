namespace HeadquartersService.Models;

public enum DispatchStatus
{
    Pending,
    Assigned,
    InProgress,
    Completed,
    Failed
}

public class DispatchRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecyclerId { get; set; }
    public int ExpectedBottles { get; set; }
    public double FullnessPercentage { get; set; }
    public double Priority { get; set; }
    public DispatchStatus Status { get; set; } = DispatchStatus.Pending;
    public Guid? AssignedTruckId { get; set; }
    public DateTime EnqueuedAtUtc { get; set; } = DateTime.UtcNow;
}