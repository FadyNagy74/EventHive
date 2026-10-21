using CatalogService.Domain.Enums;

namespace CatalogService.Domain.Entities;

public class Event
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Venue { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int TotalCapacity { get; set; }
    public int RemainingSeats { get; set; }
    public EventStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ManualCapacityAdjustment> CapacityAdjustments { get; set; } = new List<ManualCapacityAdjustment>();
}
