using FastEndpoints;
using TruckService.Services;

namespace TruckService.Endpoints.SellTruck;

public class SellTruckEndpoint(ITruckRepository repo, IHttpClientFactory httpClientFactory, ILogger<SellTruckEndpoint> logger, ITruckTelemetryStore telemetryStore)
    : Endpoint<SellTruckRequest, SellTruckEndpoint.SellTruckResponse>
{
    public override void Configure()
    {
        Post("/truck/{TruckId}/sell");
        AllowAnonymous();
        Options(x => x.WithTags("Truck"));
    }

    public override async Task HandleAsync(SellTruckRequest req, CancellationToken ct)
    {
        var idStr = Route<string>("TruckId");
        if (string.IsNullOrEmpty(idStr) || !Guid.TryParse(idStr, out var truckId))
        {
            await Send.ResultAsync(TypedResults.BadRequest("Invalid truck ID"));
            return;
        }

        var truck = await repo.GetEntityByIdAsync(truckId, ct);
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

        var telemetrySnapshot = telemetryStore.GetAll().FirstOrDefault(s => s.TruckId == truckId);
        var isTransporting = telemetrySnapshot != null &&
                             !string.Equals(telemetrySnapshot.Status, "idle", StringComparison.OrdinalIgnoreCase) &&
                             !string.Equals(telemetrySnapshot.Status, "inactive", StringComparison.OrdinalIgnoreCase);
        if (isTransporting)
        {
            AddError("Cannot sell active truck. Wait for truck to become idle.");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        truck.IsBlockedForSale = true;
        truck.BlockedForSaleAt = DateTimeOffset.UtcNow;
        await repo.UpdateAsync(truck, ct);

        const decimal salePrice = 600m;
        var creditSuccess = await CreditPlayerAsync(req.PlayerId, salePrice, $"Sold truck {truck.Model}", ct);

        if (!creditSuccess)
        {
            logger.LogError("Failed to credit player {PlayerId} for truck sale", req.PlayerId);
            AddError("Failed to credit player account");
            await Send.ErrorsAsync(500, ct);
            return;
        }

        await repo.DeleteAsync(truckId, ct);
        telemetryStore.MarkInactive(truckId);

        logger.LogInformation("Truck {TruckId} sold for {SalePrice} credits to player {PlayerId}",
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
            // Use named client so TestcontainersFixture's mock client is used in tests
            var client = httpClientFactory.CreateClient("GameService");
            var baseUrl = client.BaseAddress?.ToString()?.TrimEnd('/') ?? "http://gameservice:80";

            var response = await client.PostAsJsonAsync(
                $"{baseUrl}/player/{playerId}/deposit",
                new { PlayerId = playerId, Amount = amount, Reason = reason },
                ct);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to credit player {PlayerId}", playerId);
            return false;
        }
    }


    public class SellTruckResponse
    {
        public bool Success { get; set; }
        public Guid TruckId { get; set; }
        public string TruckModel { get; set; } = string.Empty;
        public decimal CreditsAwarded { get; set; }
    }
}