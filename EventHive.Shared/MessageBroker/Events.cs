namespace EventHive.Shared.MessageBroker
{
    public record BookingCreated(Guid BookingId = default, Guid UserId = default, Guid EventId = default, int Quantity = default, decimal AmountDue = default);
    public record PaymentSucceeded(Guid BookingId);
    public record PaymentFailed(Guid BookingId, string Reason);
    public record BookingExpired(Guid BookingId);
    public record EventCancelled(Guid EventId);
    public record ExpireBooking(Guid BookingId);
}