namespace CatalogService.DTOs
{
    public record PublishedEventDto(
        string Name,
        string Description,
        DateTime Date,
        string Venue,
        decimal Price,
        int RemainingSeats
    );
}
