using EventHive.Shared.MessageBroker;
using MassTransit;
using NotificationService.Services;

namespace NotificationService.Consumers
{
    public class BookingExpiredConsumer : IConsumer<BookingExpired>
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<BookingExpiredConsumer> _logger;

        public BookingExpiredConsumer(
            INotificationService notificationService,
            ILogger<BookingExpiredConsumer> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<BookingExpired> context)
        {
            var bookingId = context.Message.BookingId;
            _logger.LogInformation("Received BookingExpired event for BookingId: {BookingId}, MessageId: {MessageId}",
                bookingId, context.MessageId);

            await _notificationService.ExpireBookingNotificationAsync(
                bookingId,
                context.MessageId);
        }
    }
}
