using BookingService.Domain.Enums;
using BookingService.Infrastructure.Repositories;
using EventHive.Shared.MessageBroker;
using MassTransit;

namespace BookingService.Consumers
{
    public class PaymentSucceededConsumer : IConsumer<PaymentSucceeded>
    {
        private readonly IBookingRepository _bookingRepository;
        private readonly ILogger<PaymentSucceededConsumer> _logger;

        public PaymentSucceededConsumer(IBookingRepository bookingRepository, ILogger<PaymentSucceededConsumer> logger)
        {
            _bookingRepository = bookingRepository;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<PaymentSucceeded> context)
        {
            var bookingId = context.Message.BookingId;
            if (bookingId == Guid.Empty)
            {
                _logger.LogWarning("Received PaymentSucceeded with empty BookingId. Safely discarding message.");
                return;
            }

            var booking = await _bookingRepository.GetBookingByIdAsync(bookingId);
            if (booking == null)
            {
                _logger.LogWarning("Received PaymentSucceeded for unknown booking {BookingId}. Safely discarding message.", bookingId);
                return;
            }

            if (booking.Status == BookingStatus.Confirmed)
            {
                _logger.LogInformation("Booking {BookingId} is already Confirmed. Duplicate PaymentSucceeded message ignored.", bookingId);
                return;
            }

            if (booking.Status == BookingStatus.Cancelled || booking.Status == BookingStatus.Expired)
            {
                _logger.LogError(
                    "CRITICAL: Received PaymentSucceeded for booking {BookingId}, but booking is already in {Status} state! Seats were already released.",
                    bookingId, booking.Status);
                return;
            }

            bool result = await _bookingRepository.ChangeBookingStateAsync(bookingId, BookingStatus.Pending, BookingStatus.Confirmed);
            if (!result)
            {
                _logger.LogInformation("Booking state was not updated to Confirmed for booking {BookingId} (concurrent modification).", bookingId);
            }
            else
            {
                _logger.LogInformation("Booking state updated to Confirmed for booking: {BookingId}", bookingId);
            }
        }
    }
}
