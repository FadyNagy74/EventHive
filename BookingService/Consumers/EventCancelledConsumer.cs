using BookingService.Infrastructure.Repositories;
using EventHive.Shared.MessageBroker;
using MassTransit;

namespace BookingService.Consumers
{
    public class EventCancelledConsumer : IConsumer<EventCancelled>
    {
        private readonly IBookingRepository _bookingRepository;
        private readonly ILogger<EventCancelledConsumer> _logger;
        public EventCancelledConsumer(IBookingRepository bookingRepository, ILogger<EventCancelledConsumer> logger)
        {
            _bookingRepository = bookingRepository;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<EventCancelled> context)
        {
            var eventId = context.Message.EventId;
            if (eventId == Guid.Empty)
            {
                _logger.LogWarning("Received EventCancelled with empty EventId. Discarding message.");
                return;
            }

            var cancelledCount = await _bookingRepository.CancelEventBookings(eventId);
            _logger.LogInformation("EventCancelled processed: cancelled {Count} active bookings for event {EventId}.",
                cancelledCount, eventId);
        }
    }
}
