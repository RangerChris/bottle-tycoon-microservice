// filepath: d:\projects\bottle-tycoon-microservice\src\GameService\Events\DeliveryCompleted.cs

namespace GameService.Events;

public record DeliveryCompleted(
    Guid TruckId,
    Guid PlantId,
    Guid PlayerId,
    DateTimeOffset Timestamp,
    IDictionary<string, int> LoadByType,
    decimal CreditsEarned
);