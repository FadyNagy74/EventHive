namespace EventHive.Shared.MessageBroker
{
    public record BookingCreated(Guid BookingId = default, string? EventName = default, 
        Guid UserId = default, Guid EventId = default, int Quantity = default, 
        decimal UnitPrice = default, decimal AmountDue = default, DateTime EventDate = default);
    public record PaymentSucceeded(Guid BookingId);
    public record PaymentFailed(Guid BookingId, string Reason);
    public record BookingExpired(Guid BookingId);
    public record EventCancelled(Guid EventId);
    public record ExpireBooking(Guid BookingId);
}