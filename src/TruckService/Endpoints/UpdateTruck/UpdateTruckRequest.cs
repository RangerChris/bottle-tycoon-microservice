﻿namespace TruckService.Endpoints.UpdateTruck;

public class UpdateTruckRequest
{
    public Guid TruckId { get; set; }

    public Guid Id
    {
        get => TruckId;
        set => TruckId = value;
    }

    public string Model { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}