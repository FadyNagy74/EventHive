namespace EventHive.Shared.MessageBroker
{
    public record BookingCreated(Guid BookingId, Guid UserId, Guid EventId, int Quantity, decimal AmountDue);
    public record PaymentSucceeded(Guid BookingId);
    public record PaymentFailed(Guid BookingId, string Reason);
    public record BookingExpired(Guid BookingId);
    public record EventCancelled(Guid EventId);
}