﻿using FastEndpoints;
using TruckService.Models;
using TruckService.Services;

namespace TruckService.Endpoints.UpdateTruck;

public class UpdateTruckEndpoint : Endpoint<UpdateTruckRequest>
{
    private readonly ITruckRepository _repo;

    public UpdateTruckEndpoint(ITruckRepository repo)
    {
        _repo = repo;
    }

    public override void Configure()
    {
        Put("/truck");
        AllowAnonymous();
    }

    public override async Task HandleAsync(UpdateTruckRequest req, CancellationToken ct)
    {
        var dto = new TruckDto { Id = req.TruckId, Model = req.Model, IsActive = req.IsActive };
        var ok = await _repo.UpdateAsync(dto, ct);
        if (!ok)
        {
            await Send.NotFoundAsync(ct);
        }
        else
        {
            await Send.NoContentAsync(ct);
        }
    }
}