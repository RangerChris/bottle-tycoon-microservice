namespace TruckService.Endpoints.CreateTruck;

public class CreateTruckRequest
{
    public Guid PlayerId { get; set; }
    public Guid Id { get; set; }
    public string Model { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
}