using FastEndpoints;
using TruckService.Data;
using TruckService.Models;

namespace TruckService.Endpoints.UpgradeTruck;

public class UpgradeTruckEndpoint : Endpoint<UpgradeTruckRequest, TruckDto>
{
    private readonly TruckDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;

    public UpgradeTruckEndpoint(TruckDbContext db, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
    }

    public override void Configure()
    {
        Post("/truck/{TruckId}/upgrade");
        AllowAnonymous();
    }

    public override async Task HandleAsync(UpgradeTruckRequest req, CancellationToken ct)
    {
        var truck = await _db.Trucks.FindAsync([req.TruckId], ct);
        if (truck == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (truck.CapacityLevel >= 3)
        {
            AddError("Truck is already at max level");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        // Calculate cost: 300 * (current level + 1)
        var cost = 300m * (truck.CapacityLevel + 1);
        var debitSuccess = await DebitCreditsAsync(req.PlayerId, cost, $"Upgraded truck to level {truck.CapacityLevel + 1}", ct);
        if (!debitSuccess)
        {
            await Send.ErrorsAsync(400, ct);
            return;
        }

        truck.CapacityLevel++;

        // Update model name to reflect upgrade if it's the standard name
        if (truck.Model == "Standard Truck" || truck.Model.StartsWith("Standard Truck Mk"))
        {
            truck.Model = $"Standard Truck Mk {truck.CapacityLevel + 1}";
        }

        await _db.SaveChangesAsync(ct);

        var dto = new TruckDto
        {
            Id = truck.Id,
            Model = truck.Model,
            IsActive = truck.IsActive,
            Level = truck.CapacityLevel
        };

        await Send.OkAsync(dto, ct);
    }

    private async Task<bool> DebitCreditsAsync(Guid playerId, decimal amount, string reason, CancellationToken ct)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient("GameService");
            var response = await client.PostAsJsonAsync($"/player/{playerId}/deduct", new
            {
                PlayerId = playerId,
                Amount = amount,
                Reason = reason
            }, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            // If GameService is not available, return false
            return false;
        }
    }
}