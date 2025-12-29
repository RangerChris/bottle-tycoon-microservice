using FastEndpoints;
using RecyclerService.Data;
using RecyclerService.Models;

namespace RecyclerService.Endpoints;

public class CreateRecyclerEndpoint : Endpoint<CreateRecyclerEndpoint.Request, CreateRecyclerEndpoint.Response>
{
    private readonly RecyclerDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;

    public CreateRecyclerEndpoint(RecyclerDbContext db, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
    }

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

        _db.Recyclers.Add(entity);
        await _db.SaveChangesAsync(ct);

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