using EventHive.Shared.MessageBroker;
using MassTransit;
using NotificationService.Services;

namespace NotificationService.Consumers
{
    public class BookingCreatedConsumer : IConsumer<BookingCreated>
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<BookingCreatedConsumer> _logger;

        public BookingCreatedConsumer(
            INotificationService notificationService,
            ILogger<BookingCreatedConsumer> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<BookingCreated> context)
        {
            var msg = context.Message;
            _logger.LogInformation("Received BookingCreated event for BookingId: {BookingId}, MessageId: {MessageId}",
                msg.BookingId, context.MessageId);

            await _notificationService.CreatePendingBookingNotificationAsync(
                msg.BookingId,
                msg.UserId,
                msg.EventId,
                msg.EventName ?? string.Empty,
                msg.EventDate,
                msg.Quantity,
                context.MessageId);
        }
    }
}
