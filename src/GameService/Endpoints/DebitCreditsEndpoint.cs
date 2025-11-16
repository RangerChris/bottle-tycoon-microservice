using FastEndpoints;
using GameService.Services;

namespace GameService.Endpoints;

public class DebitCreditsRequest
{
    public Guid PlayerId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class DebitCreditsEndpoint : Endpoint<DebitCreditsRequest, bool>
{
    private readonly IPlayerService _playerService;

    public DebitCreditsEndpoint(IPlayerService playerService)
    {
        _playerService = playerService;
    }

    public override void Configure()
    {
        Post("/players/{PlayerId}/debit");
        AllowAnonymous();
    }

    public override async Task HandleAsync(DebitCreditsRequest req, CancellationToken ct)
    {
        if (req.Amount <= 0)
        {
            AddError("Amount must be positive");
            var err = ValidationFailures
                .GroupBy(f => f.PropertyName)
                .ToDictionary(g => g.Key ?? string.Empty, g => g.Select(f => f.ErrorMessage).ToArray());
            await Send.ResultAsync(TypedResults.BadRequest(new { errors = err }));
            return;
        }

        var success = await _playerService.DebitCreditsAsync(req.PlayerId, req.Amount, req.Reason);
        if (!success)
        {
            AddError("Insufficient credits or player not found");
            var err = ValidationFailures
                .GroupBy(f => f.PropertyName)
                .ToDictionary(g => g.Key ?? string.Empty, g => g.Select(f => f.ErrorMessage).ToArray());
            await Send.ResultAsync(TypedResults.BadRequest(new { errors = err }));
            return;
        }

        await Send.ResultAsync(TypedResults.Ok(success));
    }
}