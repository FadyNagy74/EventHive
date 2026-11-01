namespace BookingService.Services.DTOs.Responses
{
    public record ReserveSeatsResponseDto(decimal UnitPrice, decimal TotalPrice, DateTime EventDate = default);
}
