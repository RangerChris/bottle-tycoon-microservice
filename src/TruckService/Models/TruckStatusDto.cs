namespace TruckService.Models;

public class TruckStatusDto
{
    public Guid Id { get; init; }
    public string State { get; init; } = "Idle";
    public string Location { get; init; } = string.Empty;
    public Dictionary<string, int> CurrentLoadByType { get; init; } = new();
    public double MaxCapacityUnits { get; init; }
    public int CapacityLevel { get; init; }
    public decimal TotalEarnings { get; init; }
}