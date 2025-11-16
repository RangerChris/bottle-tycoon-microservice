namespace RecyclerService.Events;

public record RecyclerFull(
    Guid RecyclerId,
    int Capacity,
    int CurrentLoad,
    DateTimeOffset Timestamp
);