namespace TruckService.Endpoints.Dispatch;

public class DispatchRequest
{
    public Guid TruckId { get; set; }
    public Guid RecyclerId { get; set; }
    public double DistanceKm { get; set; }
}