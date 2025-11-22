using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace RecyclerService.Models;

public class Recycler
{
    [Key] public Guid Id { get; set; }

    public string Name { get; set; } = null!;
    public int Capacity { get; set; }
    public string BottleInventoryJson { get; set; } = "{}";
    public string? Location { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastEmptiedAt { get; set; }

    public ICollection<Visitor> Visitors { get; set; } = new List<Visitor>();

    public Dictionary<string, int> GetBottleInventory()
    {
        return JsonSerializer.Deserialize<Dictionary<string, int>>(BottleInventoryJson) ?? new Dictionary<string, int>();
    }

    public void SetBottleInventory(Dictionary<string, int> inventory)
    {
        BottleInventoryJson = JsonSerializer.Serialize(inventory);
    }

    public int CurrentLoad => GetBottleInventory().Values.Sum();
}