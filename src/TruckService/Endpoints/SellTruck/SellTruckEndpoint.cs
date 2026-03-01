using FastEndpoints;
using TruckService.Services;

namespace TruckService.Endpoints.SellTruck;

public class SellTruckEndpoint : Endpoint<SellTruckEndpoint.Request, SellTruckEndpoint.SellTruckResponse>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SellTruckEndpoint> _logger;
    private readonly ITruckRepository _repo;
    private readonly ITruckTelemetryStore _telemetryStore;

    public SellTruckEndpoint(ITruckRepository repo, IHttpClientFactory httpClientFactory, ILogger<SellTruckEndpoint> logger, ITruckTelemetryStore telemetryStore)
    {
        _repo = repo;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _telemetryStore = telemetryStore;
    }

    public override void Configure()
    {
        Post("/truck/{TruckId}/sell");
        AllowAnonymous();
        Options(x => x.WithTags("Truck"));
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var idStr = Route<string>("TruckId");
        if (string.IsNullOrEmpty(idStr) || !Guid.TryParse(idStr, out var truckId))
        {
            await Send.ResultAsync(TypedResults.BadRequest("Invalid truck ID"));
            return;
        }

        var truck = await _repo.GetEntityByIdAsync(truckId, ct);
        if (truck == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (truck.IsBlockedForSale)
        {
            AddError("Truck is already blocked for sale");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        if (!truck.IsActive)
        {
            AddError("Cannot sell active truck. Wait for truck to become idle.");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        truck.IsBlockedForSale = true;
        truck.BlockedForSaleAt = DateTimeOffset.UtcNow;
        await _repo.UpdateAsync(truck, ct);

        const decimal salePrice = 600m;
        var creditSuccess = await CreditPlayerAsync(req.PlayerId, salePrice, $"Sold truck {truck.Model}", ct);

        if (!creditSuccess)
        {
            _logger.LogError("Failed to credit player {PlayerId} for truck sale", req.PlayerId);
            AddError("Failed to credit player account");
            await Send.ErrorsAsync(500, ct);
            return;
        }

        await _repo.DeleteAsync(truckId, ct);
        _telemetryStore.Remove(truckId);

        _logger.LogInformation("Truck {TruckId} sold for {SalePrice} credits to player {PlayerId}",
            truckId, salePrice, req.PlayerId);

        await Send.OkAsync(new SellTruckResponse
        {
            Success = true,
            TruckId = truckId,
            TruckModel = truck.Model,
            CreditsAwarded = salePrice
        }, ct);
    }

    private async Task<bool> CreditPlayerAsync(Guid playerId, decimal amount, string reason, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var gameServiceUrl = "http://gameservice:80";

            var response = await client.PostAsJsonAsync(
                $"{gameServiceUrl}/player/{playerId}/deposit",
                new { PlayerId = playerId, Amount = amount, Reason = reason },
                ct);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to credit player {PlayerId}", playerId);
            return false;
        }
    }

    public class Request
    {
        public Guid PlayerId { get; set; }
    }

    public class SellTruckResponse
    {
        public bool Success { get; set; }
        public Guid TruckId { get; set; }
        public string TruckModel { get; set; } = string.Empty;
        public decimal CreditsAwarded { get; set; }
    }
}