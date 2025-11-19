namespace TruckService.Endpoints.CreateTruck;

public class CreateTruckRequest
{
    public Guid Id { get; set; }
    public string LicensePlate { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}