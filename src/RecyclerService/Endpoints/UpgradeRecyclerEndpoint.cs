using FastEndpoints;
using RecyclerService.Data;

namespace RecyclerService.Endpoints;

public class UpgradeRecyclerEndpoint : Endpoint<UpgradeRecyclerEndpoint.Request, UpgradeRecyclerEndpoint.Response>
{
    private readonly RecyclerDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;

    public UpgradeRecyclerEndpoint(RecyclerDbContext db, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
    }

    public override void Configure()
    {
        Verbs("POST");
        Routes("/recyclers/{RecyclerId}/upgrade");
        AllowAnonymous();
        Options(x => x.WithTags("Recycler"));
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var recycler = await _db.Recyclers.FindAsync([req.RecyclerId], ct);
        if (recycler == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (recycler.CapacityLevel >= 3)
        {
            AddError("Recycler is already at max level");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        // Calculate cost: 200 * (current level + 1)
        var cost = 200m * (recycler.CapacityLevel + 1);
        var debitSuccess = await DebitCreditsAsync(req.PlayerId, cost, $"Upgraded recycler to level {recycler.CapacityLevel + 1}", ct);
        if (!debitSuccess)
        {
            await Send.ErrorsAsync(400, ct);
            return;
        }

        recycler.CapacityLevel++;
        recycler.Capacity = (int)(100 * Math.Pow(1.25, recycler.CapacityLevel));

        await _db.SaveChangesAsync(ct);

        await Send.OkAsync(new Response
        {
            Id = recycler.Id,
            Name = recycler.Name,
            Capacity = recycler.Capacity,
            CapacityLevel = recycler.CapacityLevel,
            CurrentLoad = recycler.CurrentLoad,
            Location = recycler.Location
        }, ct);
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
        public Guid RecyclerId { get; set; }
    }

    public new record Response
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public int Capacity { get; set; }
        public int CapacityLevel { get; set; }
        public int CurrentLoad { get; set; }
        public string? Location { get; set; }
    }
}