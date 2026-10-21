using System.ComponentModel.DataAnnotations;

namespace BookingService.Services.DTOs.Requests
{
    public record CreateBookingDto
    (
        [Required(ErrorMessage = "The event ID is required.")]
        Guid? EventId,

        [Required(ErrorMessage = "The ticket quantity is required.")]
        [Range(1, 10, ErrorMessage = "Quantity must be between 1 and 10 tickets.")]
        int? Quantity
    );
}
