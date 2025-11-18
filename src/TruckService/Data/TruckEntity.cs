using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TruckService.Data;

[Table("trucks")]
public class TruckEntity
{
    [Key] public Guid Id { get; set; }

    [Required] public string LicensePlate { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}