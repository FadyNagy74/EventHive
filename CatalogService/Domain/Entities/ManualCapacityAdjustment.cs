namespace CatalogService.Domain.Entities;

public class ManualCapacityAdjustment
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public int ChangeDelta { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string AdjustedBy { get; set; } = string.Empty;
    public Guid AdjustedById { get; set; }
    public DateTime CreatedAt { get; set; }

    public Event? Event { get; set; }
}
