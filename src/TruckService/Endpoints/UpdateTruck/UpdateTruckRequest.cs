namespace TruckService.Endpoints.UpdateTruck;

public class UpdateTruckRequest
{
    public Guid TruckId { get; set; }
    public string Model { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}