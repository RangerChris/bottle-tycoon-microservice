﻿namespace TruckService.Endpoints.UpgradeTruck;

public class UpgradeTruckRequest
{
    public Guid PlayerId { get; set; }
    public Guid TruckId { get; set; }
}