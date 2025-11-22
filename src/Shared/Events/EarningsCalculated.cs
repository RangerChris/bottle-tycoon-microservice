namespace Shared.Events;

public record EarningsCalculated(
    Guid DeliveryId,
    Guid PlayerId,
    decimal GrossEarnings,
    decimal OperatingCost,
    decimal NetEarnings,
    DateTimeOffset CalculatedAt
);