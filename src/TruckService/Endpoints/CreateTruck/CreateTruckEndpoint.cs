﻿﻿using FastEndpoints;
using TruckService.Models;
using TruckService.Services;

namespace TruckService.Endpoints.CreateTruck;

public class CreateTruckEndpoint : Endpoint<CreateTruckRequest, TruckDto>
{
    private readonly ITruckService _service;

    public CreateTruckEndpoint(ITruckService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("/truck");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CreateTruckRequest req, CancellationToken ct)
    {
        var dto = new TruckDto { Id = req.Id, Model = req.Model, IsActive = req.IsActive };
        var created = await _service.CreateTruckAsync(dto);
        await Send.ResultAsync(TypedResults.Created($"/truck/{created.Id}", created));
    }
}