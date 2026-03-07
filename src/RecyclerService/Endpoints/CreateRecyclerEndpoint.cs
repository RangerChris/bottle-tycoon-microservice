﻿using FastEndpoints;
using RecyclerService.Data;
using RecyclerService.Models;
using RecyclerService.Services;

namespace RecyclerService.Endpoints;

public class CreateRecyclerEndpoint(RecyclerDbContext db, IHttpClientFactory httpClientFactory, IRecyclerTelemetryStore telemetryStore) : Endpoint<CreateRecyclerEndpoint.Request, CreateRecyclerEndpoint.Response>
{
    public override void Configure()
    {
        Verbs("POST");
        Routes("/recyclers");
        AllowAnonymous();
        Options(x => x.WithTags("Recycler"));
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        // Deduct credits for new recycler: 500 credits
        var debitSuccess = await DebitCreditsAsync(req.PlayerId, 500m, "Purchased new recycler", ct);
        if (!debitSuccess)
        {
            await Send.ErrorsAsync(400, ct);
            return;
        }

        var entity = new Recycler
        {
            Id = req.Id == Guid.Empty ? Guid.NewGuid() : req.Id,
            Name = req.Name,
            Capacity = req.Capacity, // Use capacity from request
            CapacityLevel = 0,
            Location = req.Location,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Recyclers.Add(entity);
        await db.SaveChangesAsync(ct);

        telemetryStore.MarkActive(entity.Id, entity.Name);
        telemetryStore.Set(entity.Id, entity.Name, 0, 0, 0);

        await Send.ResultAsync(TypedResults.Created($"/recyclers/{entity.Id}", new Response
        {
            Id = entity.Id,
            Name = entity.Name,
            Capacity = entity.Capacity,
            CurrentLoad = entity.CurrentLoad,
            Location = entity.Location
        }));
    }

    private async Task<bool> DebitCreditsAsync(Guid playerId, decimal amount, string reason, CancellationToken ct)
    {
        try
        {
            using var client = httpClientFactory.CreateClient("GameService");
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

    public record Request
    {
        public Guid PlayerId { get; set; }
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public int Capacity { get; set; }
        public string? Location { get; set; }
    }

    public new record Response
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public int Capacity { get; set; }
        public int CurrentLoad { get; set; }
        public string? Location { get; set; }
    }
}