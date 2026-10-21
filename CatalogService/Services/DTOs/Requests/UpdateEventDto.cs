using System.ComponentModel.DataAnnotations;

namespace CatalogService.Services.DTOs.Requests
{
    public record UpdateEventDto
    (
        [Required(ErrorMessage = "The event name is required.")]
        [MinLength(3, ErrorMessage = "The event name must be at least 3 characters long.")]
        string Name,

        [Required(ErrorMessage = "The event description is required.")]
        string Description,

        [Required(ErrorMessage = "The event date is required.")]
        DateTime? Date,

        [Required(ErrorMessage = "The venue is required.")]
        string Venue,

        [Required(ErrorMessage = "The ticket price is required.")]
        [Range(0, 1000000, ErrorMessage = "The price cannot be a negative number.")]
        decimal? Price,

        [Required(ErrorMessage = "The total capacity is required.")]
        [Range(1, 1000000, ErrorMessage = "The total capacity must be at least 1 seat.")]
        int? TotalCapacity 
    );
}
