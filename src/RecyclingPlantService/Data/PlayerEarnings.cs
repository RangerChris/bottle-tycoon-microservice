using System.ComponentModel.DataAnnotations;

namespace RecyclingPlantService.Data;

public class PlayerEarnings
{
    [Key] public Guid PlayerId { get; set; }

    [Required] public decimal TotalEarnings { get; set; }

    [Required] public int DeliveryCount { get; set; }

    [Required] public decimal AverageEarnings { get; set; }

    [Required] public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}