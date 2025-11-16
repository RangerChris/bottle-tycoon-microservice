namespace GameService.Models;

public class Upgrade
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public string ItemType { get; set; } = string.Empty; // "Recycler", "Truck"
    public int ItemId { get; set; } // ID of the specific recycler/truck
    public int NewLevel { get; set; }
    public decimal Cost { get; set; }
    public DateTime UpgradedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public Player Player { get; set; } = null!;
}