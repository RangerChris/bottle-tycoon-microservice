// filepath: d:\projects\bottle-tycoon-microservice\src\TruckService\Data\DeliveryEntity.cs

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace TruckService.Data;

[Table("deliveries")]
public class DeliveryEntity
{
    [Key] public Guid Id { get; set; }

    public Guid TruckId { get; set; }

    public Guid RecyclerId { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public string State { get; set; } = "Queued";
    public DateTimeOffset? CompletedAt { get; set; }

    // store loadByType as JSON
    public string LoadByTypeJson { get; set; } = string.Empty;

    public decimal GrossEarnings { get; set; }

    public decimal OperatingCost { get; set; }

    public decimal NetProfit { get; set; }

    public static string SerializeLoad(IDictionary<string, int> load)
    {
        return JsonSerializer.Serialize(load);
    }

    public IDictionary<string, int> GetLoadByType()
    {
        return JsonSerializer.Deserialize<Dictionary<string, int>>(LoadByTypeJson) ?? new Dictionary<string, int>();
    }
}