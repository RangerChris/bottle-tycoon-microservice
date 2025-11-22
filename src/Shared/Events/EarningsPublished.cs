namespace Shared.Events;

public record EarningsPublished(
    Guid DeliveryId,
    Guid PlayerId,
    decimal NetEarnings,
    DateTimeOffset PublishedAt
);