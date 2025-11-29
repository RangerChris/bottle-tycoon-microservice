namespace TruckService.Endpoints.UpdateTruck;

public class UpdateTruckRequest
{
    private Guid _truckId;
    public Guid TruckId { get => _truckId; set => _truckId = value; }
    public Guid Id { get => _truckId; set => _truckId = value; }
    public string LicensePlate { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}