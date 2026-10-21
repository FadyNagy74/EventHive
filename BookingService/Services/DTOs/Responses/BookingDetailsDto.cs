using BookingService.Domain.Enums;

namespace BookingService.Services.DTOs.Responses
{
    public record BookingDetailsDto
    (
        Guid Id,
        Guid EventId,
        Guid UserId,
        int Quantity,
        BookingStatus Status,
        decimal TotalPrice,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        string? PaymentReference = null
    );
}
