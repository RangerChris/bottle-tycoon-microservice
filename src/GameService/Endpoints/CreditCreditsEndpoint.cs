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
    private readonly IPlayerService _playerService;

    public CreditCreditsEndpoint(IPlayerService playerService)
    {
        _playerService = playerService;
    }

    public override void Configure()
    {
        Post("/player/{PlayerId:guid}/deposit");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CreditCreditsRequest req, CancellationToken ct)
    {
        // If model binding produced validation failures (for example when the route contains a non-GUID value),
        // return them as a structured 400 response early.
        if (ValidationFailures?.Any() == true)
        {
            var err = ValidationFailures
                .GroupBy(f => f.PropertyName)
                .ToDictionary(g => g.Key ?? string.Empty, g => g.Select(f => f.ErrorMessage).ToArray());
            await Send.ResultAsync(TypedResults.BadRequest(new { message = "Validation failed", errors = err }));
            return;
        }

        if (req.Amount <= 0)
        {
            AddError("Amount must be positive");
            if (ValidationFailures != null)
            {
                var err = ValidationFailures
                    .GroupBy(f => f.PropertyName)
                    .ToDictionary(g => g.Key ?? string.Empty, g => g.Select(f => f.ErrorMessage).ToArray());
                await Send.ResultAsync(TypedResults.BadRequest(new { errors = err }));
            }

            return;
        }

        var success = await _playerService.CreditCreditsAsync(req.PlayerId, req.Amount, req.Reason);
        if (!success)
        {
            AddError("Player not found");
            if (ValidationFailures != null)
            {
                var err = ValidationFailures
                    .GroupBy(f => f.PropertyName)
                    .ToDictionary(g => g.Key ?? string.Empty, g => g.Select(f => f.ErrorMessage).ToArray());
                await Send.ResultAsync(TypedResults.BadRequest(new { errors = err }));
            }

            return;
        }

        await Send.ResultAsync(TypedResults.Ok(success));
    }
}