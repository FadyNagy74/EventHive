namespace NotificationService.Services.DTOs.Responses
{
    public record NotificationResponseDto(
        Guid Id,
        Guid BookingId,
        Guid UserId,
        string Type,
        string Message,
        string Channel,
        DateTime SentAt
    );
}
