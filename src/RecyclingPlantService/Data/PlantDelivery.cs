using System.ComponentModel.DataAnnotations;

namespace RecyclingPlantService.Data;

public class PlantDelivery
{
    [Key] public Guid Id { get; init; }

    [Required] public Guid TruckId { get; init; }

    [Required] public Guid PlayerId { get; init; } // Assuming we get this from somewhere, maybe need to add to event

    [Required] public int GlassCount { get; init; }

    [Required] public int MetalCount { get; init; }

    [Required] public int PlasticCount { get; init; }

    [Required] public decimal GrossEarnings { get; init; }

    [Required] public decimal OperatingCost { get; init; }

    [Required] public decimal NetEarnings { get; init; }

    [Required] public DateTimeOffset DeliveredAt { get; init; }

    [Required] public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}