using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using TruckService.Data;
using TruckService.Services;

namespace TruckService.Endpoints;

public sealed class TruckTelemetryEndpoint(TruckDbContext db, ITruckTelemetryStore telemetryStore, ILogger<TruckTelemetryEndpoint> logger) : Endpoint<TruckTelemetryEndpoint.Request, TruckTelemetryEndpoint.Response>
{
    public override void Configure()
    {
        Post("/trucks/{id:guid}/telemetry");
        AllowAnonymous();
        Options(x => x.WithTags("Truck"));
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var truckId = Route<Guid>("id");
        var truck = await db.Trucks.FirstOrDefaultAsync(t => t.Id == truckId, ct);
        if (truck == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var capacity = req.Capacity ?? 0;
        var currentLoad = req.CurrentLoad ?? 0;
        var status = req.Status ?? "idle";

        telemetryStore.Set(truckId, truck.Model, currentLoad, capacity, status);
        logger.LogInformation("Telemetry updated for truck {TruckId}: load={CurrentLoad}, capacity={Capacity}, status={Status}",
            truckId, currentLoad, capacity, status);

        await Send.OkAsync(new Response { TruckId = truckId, CurrentLoad = currentLoad, Capacity = capacity, Status = status }, ct);
    }

    public sealed record Request
    {
        public int? CurrentLoad { get; init; }
        public int? Capacity { get; init; }
        public string? Status { get; init; }
    }

    public new sealed record Response
    {
        public Guid TruckId { get; init; }
        public int CurrentLoad { get; init; }
        public int Capacity { get; init; }
        public string Status { get; init; } = "idle";
    }
}