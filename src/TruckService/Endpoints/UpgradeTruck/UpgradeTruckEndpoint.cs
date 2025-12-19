﻿using FastEndpoints;
using TruckService.Data;
using TruckService.Models;

namespace TruckService.Endpoints.UpgradeTruck;

public class UpgradeTruckEndpoint : Endpoint<UpgradeTruckRequest, TruckDto>
{
    private readonly TruckDbContext _db;

    public UpgradeTruckEndpoint(TruckDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/truck/{TruckId}/upgrade");
        AllowAnonymous();
    }

    public override async Task HandleAsync(UpgradeTruckRequest req, CancellationToken ct)
    {
        var truck = await _db.Trucks.FindAsync(new object[] { req.TruckId }, ct);
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
}