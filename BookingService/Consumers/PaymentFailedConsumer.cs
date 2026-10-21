using BookingService.Domain.Enums;
using BookingService.Infrastructure.Api;
using BookingService.Infrastructure.Repositories;
using EventHive.Shared.MessageBroker;
using MassTransit;

namespace BookingService.Consumers
{
    public class PaymentFailedConsumer : IConsumer<PaymentFailed>
    {
        private readonly IBookingRepository _bookingRepository;
        private readonly ICatalogApi _catalogApi;
        private readonly ILogger<PaymentFailedConsumer> _logger;

        public PaymentFailedConsumer(
            IBookingRepository bookingRepository,
            ICatalogApi catalogApi,
            ILogger<PaymentFailedConsumer> logger)
        {
            _bookingRepository = bookingRepository;
            _catalogApi = catalogApi;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<PaymentFailed> context)
        {
            var bookingId = context.Message.BookingId;
            var reason = context.Message.Reason;

            if (bookingId == Guid.Empty)
            {
                _logger.LogWarning("Received PaymentFailed with empty BookingId. Safely discarding message.");
                return;
            }

            var booking = await _bookingRepository.GetBookingByIdAsync(bookingId);
            if (booking == null)
            {
                _logger.LogWarning(
                    "Received PaymentFailed for unknown booking {BookingId}. Reason: {Reason}. Safely discarding message.",
                    bookingId, reason);
                return;
            }

            if (booking.Status == BookingStatus.Cancelled)
            {
                _logger.LogInformation("Booking {BookingId} is already Cancelled. Duplicate PaymentFailed message ignored.", bookingId);
                return;
            }

            if (booking.Status == BookingStatus.Expired)
            {
                _logger.LogInformation("Booking {BookingId} is already Expired. Duplicate PaymentFailed message ignored.", bookingId);
                return;
            }

            if (booking.Status == BookingStatus.Confirmed)
            {
                _logger.LogWarning("Received PaymentFailed for already Confirmed booking {BookingId}. Discarding message.", bookingId);
                return;
            }

            bool result = await _bookingRepository.ChangeBookingStateAsync(bookingId, BookingStatus.Pending, BookingStatus.Cancelled);
            if (!result)
            {
                _logger.LogInformation("Booking {BookingId} was not in Pending state. State change skipped.", bookingId);
                return;
            }

            _logger.LogInformation(
                "Booking {BookingId} cancelled due to payment failure. Reason: {Reason}. Releasing {Quantity} seats for Event {EventId}.",
                bookingId, reason, booking.Quantity, booking.EventId);

            var releaseResult = await _catalogApi.ReleaseSeatsAsync(booking.EventId, booking.Quantity);
            if (!releaseResult.IsSuccess)
            {
                _logger.LogError(
                    "Failed to release seats for booking {BookingId} after payment failure. EventId: {EventId}, Quantity: {Quantity}. Errors: {Errors}",
                    bookingId, booking.EventId, booking.Quantity, releaseResult.Errors);
            }
        }
    }
}
