namespace GameService.Models;

public class Purchase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public string ItemType { get; set; } = string.Empty; // "Recycler", "Truck"
    public decimal Amount { get; set; }
    public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public Player Player { get; set; } = null!;
}