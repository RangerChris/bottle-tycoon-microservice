namespace TruckService.Models;

public class TruckDto
{
    public Guid Id { get; set; }
    public string Model { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int Level { get; set; }
}