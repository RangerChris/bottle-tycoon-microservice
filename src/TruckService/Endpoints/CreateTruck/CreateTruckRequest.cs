﻿namespace TruckService.Endpoints.CreateTruck;

public class CreateTruckRequest
{
    public Guid Id { get; set; }
    public string Model { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}