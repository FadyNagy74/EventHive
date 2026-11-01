using EventHive.Shared.MessageBroker;
using MassTransit;
using NotificationService.Services;

namespace NotificationService.Consumers
{
    public class PaymentFailedConsumer : IConsumer<PaymentFailed>
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<PaymentFailedConsumer> _logger;

        public PaymentFailedConsumer(
            INotificationService notificationService,
            ILogger<PaymentFailedConsumer> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<PaymentFailed> context)
        {
            var bookingId = context.Message.BookingId;
            _logger.LogInformation("Received PaymentFailed event for BookingId: {BookingId}, Reason: {Reason}",
                bookingId, context.Message.Reason);

            await _notificationService.CancelPendingNotificationAsync(
                bookingId,
                context.Message.Reason,
                context.MessageId);
        }
    }
}
