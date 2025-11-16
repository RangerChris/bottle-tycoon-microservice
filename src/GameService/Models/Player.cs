namespace GameService.Models;

public class Player
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public decimal Credits { get; set; } = 1000; // Starting credits
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
    public ICollection<Upgrade> Upgrades { get; set; } = new List<Upgrade>();
}