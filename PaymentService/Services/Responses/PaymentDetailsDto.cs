namespace PaymentService.Services.Responses
{
    public record PaymentDetailsDto(
        Guid Id,
        Guid BookingId,
        Guid UserId,
        string Status,
        decimal Amount,
        string? ClientSecret,
        string? CheckoutUrl,
        DateTime CreatedAt,
        DateTime UpdatedAt
    );
}
