using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace RecyclerService.Models;

public class Visitor
{
    [Key] public Guid Id { get; set; }

    public Guid RecyclerId { get; set; }
    public string? VisitorType { get; set; }
    public string BottleCountsJson { get; set; } = "{}";
    public DateTimeOffset ArrivedAt { get; set; }

    public Recycler? Recycler { get; set; }

    public int Bottles => GetBottleCounts().Values.Sum();

    public Dictionary<string, int> GetBottleCounts()
    {
        return JsonSerializer.Deserialize<Dictionary<string, int>>(BottleCountsJson) ?? new Dictionary<string, int>();
    }

    public void SetBottleCounts(Dictionary<string, int> counts)
    {
        BottleCountsJson = JsonSerializer.Serialize(counts);
    }
}