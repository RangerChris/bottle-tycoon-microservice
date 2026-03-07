﻿using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using RecyclerService.Data;
using RecyclerService.Services;

namespace RecyclerService.Endpoints;

public class SellRecyclerEndpoint(RecyclerDbContext db, IHttpClientFactory httpClientFactory, ILogger<SellRecyclerEndpoint> logger, IRecyclerTelemetryStore telemetryStore)
    : Endpoint<SellRecyclerEndpoint.Request, SellRecyclerEndpoint.SellRecyclerResponse>
{
    public override void Configure()
    {
        Post("/recyclers/{RecyclerId}/sell");
        AllowAnonymous();
        Options(x => x.WithTags("Recycler"));
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var recycler = await db.Recyclers
            .Include(r => r.Customers)
            .FirstOrDefaultAsync(r => r.Id == req.RecyclerId, ct);

        if (recycler == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (recycler.IsBlockedForSale)
        {
            AddError("Recycler is already blocked for sale");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        if (recycler.Customers.Any())
        {
            AddError($"Cannot sell recycler with active customers. {recycler.Customers.Count} customer(s) in queue.");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        recycler.IsBlockedForSale = true;
        recycler.BlockedForSaleAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        const decimal salePrice = 400m;
        var creditSuccess = await CreditPlayerAsync(req.PlayerId, salePrice, $"Sold recycler {recycler.Name}", ct);

        if (!creditSuccess)
        {
            logger.LogError("Failed to credit player {PlayerId} for recycler sale", req.PlayerId);
            AddError("Failed to credit player account");
            await Send.ErrorsAsync(500, ct);
            return;
        }

        db.Recyclers.Remove(recycler);
        await db.SaveChangesAsync(ct);
        telemetryStore.MarkInactive(recycler.Id);

        logger.LogInformation("Recycler {RecyclerId} sold for {SalePrice} credits to player {PlayerId}",
            recycler.Id, salePrice, req.PlayerId);

        await Send.OkAsync(new SellRecyclerResponse
        {
            Success = true,
            RecyclerId = recycler.Id,
            RecyclerName = recycler.Name,
            CreditsAwarded = salePrice
        }, ct);
    }

    private async Task<bool> CreditPlayerAsync(Guid playerId, decimal amount, string reason, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("GameService");
            var baseUrl = client.BaseAddress?.ToString().TrimEnd('/') ?? "http://gameservice:80";

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

    public class Request
    {
        public Guid RecyclerId { get; set; }
        public Guid PlayerId { get; set; }
    }

    public class SellRecyclerResponse
    {
        public bool Success { get; set; }
        public Guid RecyclerId { get; set; }
        public string RecyclerName { get; set; } = string.Empty;
        public decimal CreditsAwarded { get; set; }
    }
}