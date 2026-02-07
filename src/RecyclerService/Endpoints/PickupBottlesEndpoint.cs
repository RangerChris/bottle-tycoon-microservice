using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using RecyclerService.Data;
using RecyclerService.Services;

namespace RecyclerService.Endpoints;

public class PickupBottlesRequest
{
    public Guid RecyclerId { get; set; }
    public int MaxCapacity { get; set; }
}

public class PickupBottlesResponse
{
    public Dictionary<string, int> BottlesPickedUp { get; set; } = new();
    public int TotalPickedUp { get; set; }
    public Dictionary<string, int> RemainingBottles { get; set; } = new();
}

public class PickupBottlesEndpoint : Endpoint<PickupBottlesRequest, PickupBottlesResponse>
{
    private readonly RecyclerDbContext _db;
    private readonly ILogger<PickupBottlesEndpoint> _logger;
    private readonly IRecyclerService _recyclerService;

    public PickupBottlesEndpoint(RecyclerDbContext db, ILogger<PickupBottlesEndpoint> logger, IRecyclerService recyclerService)
    {
        _db = db;
        _logger = logger;
        _recyclerService = recyclerService;
    }

    public override void Configure()
    {
        Post("/recyclers/{RecyclerId}/pickup");
        AllowAnonymous();
    }

    public override async Task HandleAsync(PickupBottlesRequest req, CancellationToken ct)
    {
        var recycler = await _db.Recyclers.FirstOrDefaultAsync(r => r.Id == req.RecyclerId, ct);
        if (recycler == null)
        {
            ThrowError("Recycler not found", 404);
            return;
        }

        var currentInventory = recycler.GetBottleInventory();
        var pickedUp = new Dictionary<string, int>();
        var remaining = 0;

        foreach (var type in new[] { "glass", "metal", "plastic" })
        {
            var available = currentInventory.GetValueOrDefault(type, 0);
            var toPickup = Math.Min(available, req.MaxCapacity - remaining);

            if (toPickup > 0)
            {
                pickedUp[type] = toPickup;
                currentInventory[type] = available - toPickup;
                remaining += toPickup;
            }
        }

        recycler.SetBottleInventory(currentInventory);
        recycler.LastEmptiedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _recyclerService.RecordBottlesProcessedAsync(pickedUp, ct);

        _logger.LogInformation("Picked up {Total} bottles from Recycler {RecyclerId}: Glass={Glass}, Metal={Metal}, Plastic={Plastic}",
            remaining, req.RecyclerId, pickedUp.GetValueOrDefault("glass", 0), pickedUp.GetValueOrDefault("metal", 0), pickedUp.GetValueOrDefault("plastic", 0));

        await Send.OkAsync(new PickupBottlesResponse
        {
            BottlesPickedUp = pickedUp,
            TotalPickedUp = remaining,
            RemainingBottles = currentInventory
        }, ct);
    }
}