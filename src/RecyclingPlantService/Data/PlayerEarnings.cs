using System.ComponentModel.DataAnnotations;

namespace RecyclingPlantService.Data;

public class PlayerEarnings
{
    [Key] public Guid PlayerId { get; set; }

    [Required] public decimal TotalEarnings { get; set; } = 0;

    [Required] public int DeliveryCount { get; set; } = 0;

    [Required] public decimal AverageEarnings { get; set; } = 0;

    [Required] public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}