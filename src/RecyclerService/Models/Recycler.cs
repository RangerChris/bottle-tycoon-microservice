using System.ComponentModel.DataAnnotations;

namespace RecyclerService.Models;

public class Recycler
{
    [Key] public Guid Id { get; set; }

    public string Name { get; set; } = null!;
    public int Capacity { get; set; }
    public int CurrentLoad { get; set; }
    public string? Location { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastEmptiedAt { get; set; }

    public ICollection<Visitor> Visitors { get; set; } = new List<Visitor>();
}