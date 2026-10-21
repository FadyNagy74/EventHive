namespace CatalogService.Services.DTOs.Responses
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
