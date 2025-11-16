namespace GameService.Events;

public record CreditsCredited(Guid PlayerId, decimal Amount, string Reason);