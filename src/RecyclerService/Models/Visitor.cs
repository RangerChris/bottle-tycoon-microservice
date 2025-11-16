using System.ComponentModel.DataAnnotations;

namespace RecyclerService.Models;

public class Visitor
{
    [Key] public Guid Id { get; set; }

    public Guid RecyclerId { get; set; }
    public string? VisitorType { get; set; }
    public int Bottles { get; set; }
    public DateTimeOffset ArrivedAt { get; set; }

    public Recycler? Recycler { get; set; }
}