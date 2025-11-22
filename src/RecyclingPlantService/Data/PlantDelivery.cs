using System.ComponentModel.DataAnnotations;

namespace RecyclingPlantService.Data;

public class PlantDelivery
{
    [Key] public Guid Id { get; set; }

    [Required] public Guid TruckId { get; set; }

    [Required] public Guid PlayerId { get; set; } // Assuming we get this from somewhere, maybe need to add to event

    [Required] public int GlassCount { get; set; }

    [Required] public int MetalCount { get; set; }

    [Required] public int PlasticCount { get; set; }

    [Required] public decimal GrossEarnings { get; set; }

    [Required] public decimal OperatingCost { get; set; }

    [Required] public decimal NetEarnings { get; set; }

    [Required] public DateTimeOffset DeliveredAt { get; set; }

    [Required] public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}