using BookingService.Domain.Enums;

namespace BookingService.Services.DTOs.Responses
{
    public record BookingResponseDto
    (
        Guid Id,
        Guid EventId,
        string EventName,
        Guid UserId,
        int Quantity,
        BookingStatus Status,
        decimal TotalPrice,
        decimal UnitPrice,
        DateTime CreatedAt,
        DateTime UpdatedAt
    );
}
