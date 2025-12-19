﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace TruckService.Data;

[Table("trucks")]
public class TruckEntity
{
    [Key] public Guid Id { get; set; }


    public string Model { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public int CapacityLevel { get; set; }
    public string CurrentLoadByTypeJson { get; set; } = "{}";
    public decimal TotalEarnings { get; set; }

    public Dictionary<string, int> GetCurrentLoadByType()
    {
        return JsonSerializer.Deserialize<Dictionary<string, int>>(CurrentLoadByTypeJson) ?? new Dictionary<string, int>();
    }

    public void SetCurrentLoadByType(Dictionary<string, int> load)
    {
        CurrentLoadByTypeJson = JsonSerializer.Serialize(load);
    }
}