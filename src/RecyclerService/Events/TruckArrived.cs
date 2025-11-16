namespace RecyclerService.Events;

public record TruckArrived(
    Guid TruckId,
    Guid RecyclerId,
    DateTimeOffset ArrivedAt
);