using EventHive.Shared.MessageBroker;
using MassTransit;
using NotificationService.Services;

namespace NotificationService.Consumers
{
    public class EventCancelledConsumer : IConsumer<EventCancelled>
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<EventCancelledConsumer> _logger;

        public EventCancelledConsumer(
            INotificationService notificationService,
            ILogger<EventCancelledConsumer> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<EventCancelled> context)
        {
            var eventId = context.Message.EventId;
            _logger.LogInformation("Received EventCancelled event for EventId: {EventId}, MessageId: {MessageId}",
                eventId, context.MessageId);

            await _notificationService.HandleEventCancelledNotificationAsync(eventId, context.MessageId);
        }
    }
}
