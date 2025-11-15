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
        Post("/players/{PlayerId}/credit");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CreditCreditsRequest req, CancellationToken ct)
    {
        if (req.Amount <= 0)
        {
            AddError("Amount must be positive");
            await SendErrorsAsync();
            return;
        }

        var success = await _playerService.CreditCreditsAsync(req.PlayerId, req.Amount, req.Reason);
        if (!success)
        {
            AddError("Player not found");
            await SendErrorsAsync();
            return;
        }

        await SendAsync(success, cancellation: ct);
    }
}