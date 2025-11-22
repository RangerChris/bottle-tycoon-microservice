namespace TruckService.Models;

public class TruckStatusDto
{
    public Guid Id { get; set; }
    public string LicensePlate { get; set; } = string.Empty;
    public string State { get; set; } = "Idle";
    public string Location { get; set; } = string.Empty;
    public Dictionary<string, int> CurrentLoadByType { get; set; } = new();
    public double MaxCapacityUnits { get; set; }
    public int CapacityLevel { get; set; }
    public decimal TotalEarnings { get; set; }
}