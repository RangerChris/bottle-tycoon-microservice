namespace RecyclerService.Events;

public record TruckLoaded(
    Guid TruckId,
    Guid RecyclerId,
    int LoadedBottles,
    DateTimeOffset LoadedAt
);