using FastEndpoints;
using System.Net.Http.Json;
using TruckService.Models;
using TruckService.Services;

namespace TruckService.Endpoints.CreateTruck;

public class CreateTruckEndpoint : Endpoint<CreateTruckRequest, TruckDto>
{
    private readonly ITruckService _service;
    private readonly IHttpClientFactory _httpClientFactory;

    public CreateTruckEndpoint(ITruckService service, IHttpClientFactory httpClientFactory)
    {
        _service = service;
        _httpClientFactory = httpClientFactory;
    }

    public override void Configure()
    {
        Post("/truck");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CreateTruckRequest req, CancellationToken ct)
    {
        // Deduct credits for new truck: 800 credits
        var debitSuccess = await DebitCreditsAsync(req.PlayerId, 800m, "Purchased new truck", ct);
        if (!debitSuccess)
        {
            await Send.ErrorsAsync(400, ct);
            return;
        }

        var dto = new TruckDto { Id = req.Id, Model = req.Model, IsActive = req.IsActive };
        var created = await _service.CreateTruckAsync(dto);
        await Send.ResultAsync(TypedResults.Created($"/truck/{created.Id}", created));
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