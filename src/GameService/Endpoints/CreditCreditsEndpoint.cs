using FastEndpoints;
using GameService.Services;

namespace GameService.Endpoints;

public class CreditCreditsRequest
{
    public Guid PlayerId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class CreditCreditsEndpoint : Endpoint<CreditCreditsRequest, bool>
{
    private readonly IPlayerService _player_service;

    public CreditCreditsEndpoint(IPlayerService playerService)
    {
        _player_service = playerService;
    }

    public override void Configure()
    {
        Post("/players/{PlayerId}/credit");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CreditCreditsRequest req, CancellationToken ct)
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

        var success = await _player_service.CreditCreditsAsync(req.PlayerId, req.Amount, req.Reason);
        if (!success)
        {
            AddError("Player not found");
            var err = ValidationFailures
                .GroupBy(f => f.PropertyName)
                .ToDictionary(g => g.Key ?? string.Empty, g => g.Select(f => f.ErrorMessage).ToArray());
            await Send.ResultAsync(TypedResults.BadRequest(new { errors = err }));
            return;
        }

        await Send.ResultAsync(TypedResults.Ok(success));
    }
}