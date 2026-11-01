using BookingService.Domain.Enums;
using BookingService.Infrastructure.Api;
using BookingService.Infrastructure.Repositories;
using EventHive.Shared.MessageBroker;
using MassTransit;

namespace BookingService.Consumers
{
    public class ExpireBookingConsumer : IConsumer<ExpireBooking>
    {
        private readonly IBookingRepository _bookingRepository;
        private readonly ILogger<ExpireBookingConsumer> _logger;
        private readonly ICatalogApi _catalogApi;

        public ExpireBookingConsumer(IBookingRepository bookingRepository, ILogger<ExpireBookingConsumer> logger, ICatalogApi catalogApi)
        {
            _bookingRepository = bookingRepository;
            _logger = logger;
            _catalogApi = catalogApi;
        }   
        public async Task Consume(ConsumeContext<ExpireBooking> context)
        {
            var bookingId = context.Message.BookingId;
            if (bookingId == Guid.Empty)
            {
                _logger.LogWarning("Received ExpireBooking with empty BookingId. Safely discarding message.");
                return;
            }

            var booking = await _bookingRepository.GetBookingByIdAsync(bookingId);
            if (booking == null)
            {
                _logger.LogWarning("Received ExpireBooking for unknown booking {BookingId}. Safely discarding message.", bookingId);
                return;
            }

            if (booking.Status == BookingStatus.Expired)
            {
                _logger.LogInformation("Booking {BookingId} is already expired. Re-publishing BookingExpired in case it was missed.", bookingId);
                await context.Publish(new BookingExpired(bookingId), ctx => ctx.SetRoutingKey("booking.expired"));
                return;
            }

            if (booking.Status == BookingStatus.Confirmed || booking.Status == BookingStatus.Cancelled)
            {
                _logger.LogInformation("Booking {BookingId} has already resolved to {Status}. No expiration needed.", bookingId, booking.Status);
                return;
            }

            if (booking.Status == BookingStatus.Pending)
            {
                var result = await _bookingRepository.ChangeBookingStateAsync(bookingId, BookingStatus.Pending, BookingStatus.Expired);
                if (!result)
                {
                    _logger.LogInformation("Booking {BookingId} was concurrently modified and could not be transitioned from Pending to Expired. No action taken.", bookingId);
                    return;
                }

                _logger.LogInformation("Booking {BookingId} status changed from Pending to Expired. Releasing seats.", bookingId);
                var releasedSeats = await _catalogApi.ReleaseSeatsAsync(booking.EventId, booking.Quantity);
                if (!releasedSeats.IsSuccess)
                {
                    _logger.LogError("Failed to release seats for expired booking {BookingId}.", bookingId);
                }

                await context.Publish(new BookingExpired(bookingId), ctx => ctx.SetRoutingKey("booking.expired"));
                _logger.LogInformation("Published BookingExpired event for booking {BookingId}.", bookingId);
            }
        }
    }
}

