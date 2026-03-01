namespace GameService.Models;

public class Player
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
    public decimal Credits { get; set; } = 1300; // Starting credits
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}