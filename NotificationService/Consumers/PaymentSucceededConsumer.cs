using EventHive.Shared.MessageBroker;
using MassTransit;
using NotificationService.Services;

namespace NotificationService.Consumers
{
    public class PaymentSucceededConsumer : IConsumer<PaymentSucceeded>
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<PaymentSucceededConsumer> _logger;

        public PaymentSucceededConsumer(
            INotificationService notificationService,
            ILogger<PaymentSucceededConsumer> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<PaymentSucceeded> context)
        {
            var bookingId = context.Message.BookingId;
            _logger.LogInformation("Received PaymentSucceeded event for BookingId: {BookingId}, MessageId: {MessageId}",
                bookingId, context.MessageId);

            await _notificationService.ConfirmAndSendBookingNotificationAsync(
                bookingId,
                context.MessageId);
        }
    }
}
