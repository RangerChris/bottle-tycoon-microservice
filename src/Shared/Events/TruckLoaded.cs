namespace Shared.Events;

public record TruckLoaded(
    Guid TruckId,
    Guid RecyclerId,
    Guid PlayerId,
    IDictionary<string, int> LoadByType,
    decimal OperatingCost,
    DateTimeOffset LoadedAt
);