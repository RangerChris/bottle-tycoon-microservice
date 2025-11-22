namespace Shared.Events;

public record DeliveryCompleted(
    Guid TruckId,
    Guid PlantId,
    IDictionary<string, int> LoadByType,
    decimal CreditsEarned,
    DateTimeOffset DeliveredAt
);