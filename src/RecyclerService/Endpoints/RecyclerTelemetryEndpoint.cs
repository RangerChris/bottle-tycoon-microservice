using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using RecyclerService.Data;
using RecyclerService.Services;

namespace RecyclerService.Endpoints;

public sealed class RecyclerTelemetryEndpoint : Endpoint<RecyclerTelemetryEndpoint.Request, RecyclerTelemetryEndpoint.Response>
{
    private readonly RecyclerDbContext _db;
    private readonly ILogger<RecyclerTelemetryEndpoint> _logger;
    private readonly IRecyclerTelemetryStore _telemetryStore;

    public RecyclerTelemetryEndpoint(RecyclerDbContext db, IRecyclerTelemetryStore telemetryStore, ILogger<RecyclerTelemetryEndpoint> logger)
    {
        _db = db;
        _telemetryStore = telemetryStore;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/recyclers/{id:guid}/telemetry");
        AllowAnonymous();
        Options(x => x.WithTags("Recycler"));
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var recyclerId = Route<Guid>("id");
        var recycler = await _db.Recyclers.FirstOrDefaultAsync(r => r.Id == recyclerId, ct);
        if (recycler == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var total = req.TotalBottles ?? 0;
        if (req.BottleCounts is { Count: > 0 })
        {
            total = req.BottleCounts.Values.Where(v => v > 0).Sum();
        }

        var visitorCount = req.VisitorCount ?? 0;

        _telemetryStore.Set(recyclerId, recycler.Name, total, visitorCount);
        _logger.LogInformation("Telemetry updated for recycler {RecyclerId} with {TotalBottles} bottles and {VisitorCount} visitors", recyclerId, total, visitorCount);

        await Send.OkAsync(new Response { RecyclerId = recyclerId, CurrentBottles = total, CurrentVisitors = visitorCount }, ct);
    }

    public sealed record Request
    {
        public int? TotalBottles { get; init; }
        public Dictionary<string, int>? BottleCounts { get; init; }
        public int? VisitorCount { get; init; }
    }

    public new sealed record Response
    {
        public Guid RecyclerId { get; init; }
        public int CurrentBottles { get; init; }
        public int CurrentVisitors { get; init; }
    }
}