using BookingService.Domain.Entities;
using BookingService.Domain.Enums;
using BookingService.Infrastructure.Api;
using BookingService.Infrastructure.Repositories;
using BookingService.Services.DTOs.Requests;
using BookingService.Services.DTOs.Responses;
using EventHive.Shared.Common;
using EventHive.Shared.MessageBroker;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace BookingService.Services
{
    public class BookingService : IBookingService
    {
        public readonly IBookingRepository _bookingRepository;
        public readonly ICatalogApi _catalogApi;
        public readonly ILogger<BookingService> _logger;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IMessageScheduler _messageScheduler;

        public BookingService(
            IBookingRepository bookingRepository,
            ICatalogApi catalogApi,
            ILogger<BookingService> logger,
            IPublishEndpoint publishEndpoint,
            IMessageScheduler messageScheduler)
        {
            _bookingRepository = bookingRepository;
            _catalogApi = catalogApi;
            _logger = logger;
            _publishEndpoint = publishEndpoint;
            _messageScheduler = messageScheduler;
        }

        public async Task<Result<BookingResponseDto>> CreateBooking(CreateBookingDto dto, Guid userId)
        {
            if (!dto.EventId.HasValue || dto.EventId == Guid.Empty)
            {
                return Result<BookingResponseDto>.Failure(Error.Validation("A valid Event ID is required."));
            }

            if (!dto.Quantity.HasValue || dto.Quantity.Value <= 0)
            {
                return Result<BookingResponseDto>.Failure(Error.Validation("Quantity must be greater than zero."));
            }

            if (userId == Guid.Empty)
            {
                return Result<BookingResponseDto>.Failure(Error.Validation("A valid User ID is required."));
            }

            var hasPending = await _bookingRepository.HasActivePendingBookingAsync(userId, dto.EventId.Value);
            if (hasPending)
            {
                return Result<BookingResponseDto>.Failure(
                    Error.Conflict("You already have an active pending booking for this event. Please complete or cancel it before creating a new one."));
            }

            var result = await _catalogApi.ReserveSeatsAsync(dto.EventId.Value, dto.Quantity.Value);
            if (!result.IsSuccess)
            {
                return Result<BookingResponseDto>.Failure(result.Errors);
            }
            Booking newBooking = new Booking
            {
                EventId = dto.EventId.Value,
                UserId = userId,
                Quantity = dto.Quantity.Value,
                Status = BookingStatus.Pending,
                TotalPrice = result.Value!.TotalPrice
            };
            try
            {

                await _bookingRepository.CreateBookingAsync(newBooking);

                //RabbitMq
                await _publishEndpoint.Publish(new BookingCreated
                {
                    BookingId = newBooking.Id,
                    UserId = userId,
                    EventId = dto.EventId.Value,
                    Quantity = dto.Quantity.Value,
                    AmountDue = result.Value!.TotalPrice
                }, context => context.SetRoutingKey("booking.created"));

                await _messageScheduler.SchedulePublish(
                    TimeSpan.FromMinutes(15),
                    new ExpireBooking(newBooking.Id));

                var responseDto = new BookingResponseDto(
                    newBooking.Id,
                    dto.EventId.Value,
                    userId,
                    dto.Quantity.Value,
                    newBooking.Status,
                    newBooking.TotalPrice,
                    newBooking.CreatedAt,
                    newBooking.UpdatedAt
                );

                return Result<BookingResponseDto>.Success(responseDto);
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UQ_Bookings_UserId_EventId_Pending") == true)
            {
                await _catalogApi.ReleaseSeatsAsync(dto.EventId.Value, dto.Quantity.Value);
                _logger.LogWarning("Duplicate booking attempt for UserId {UserId} EventId {EventId}. Unique constraint prevented double-booking.", userId, dto.EventId.Value);
                return Result<BookingResponseDto>.Failure(Error.Conflict("You already have an active pending booking for this event."));
            }
            catch (Exception ex)
            {
                await _catalogApi.ReleaseSeatsAsync(dto.EventId.Value, dto.Quantity.Value);
                _logger.LogError(ex, "Booking failed to save. Seats released.");
                return Result<BookingResponseDto>.Failure(Error.Failure("Booking failed, try again."));
            }

        }

        public async Task<Result<BookingDetailsDto>> GetBookingById(Guid bookingId, Guid userId)
        {
            if (bookingId == Guid.Empty)
            {
                return Result<BookingDetailsDto>.Failure(Error.Validation("A valid booking ID is required."));
            }

            if (userId == Guid.Empty)
            {
                return Result<BookingDetailsDto>.Failure(Error.Validation("A valid user ID is required."));
            }

            Booking? booking = await _bookingRepository.GetBookingByIdAsync(bookingId);
            if (booking == null || booking.UserId != userId)
            {
                return Result<BookingDetailsDto>.Failure(Error.NotFound("Booking not found."));
            }

            BookingDetailsDto bookingDetails = new BookingDetailsDto(
                booking.Id,
                booking.EventId,
                booking.UserId,
                booking.Quantity,
                booking.Status,
                booking.TotalPrice,
                booking.CreatedAt,
                booking.UpdatedAt,
                PaymentReference: null
            );

            return Result<BookingDetailsDto>.Success(bookingDetails);
        }

        public async Task<Result<PagedResponse<BookingDetailsDto>>> GetBookings(Guid userId, int pageNumber = 1, int pageSize = 20)
        {
            if (pageNumber <= 0) return Result<PagedResponse<BookingDetailsDto>>.Failure(Error.Validation("Page number must be greater than 0."));
            if (pageSize <= 0 || pageSize > 100) return Result<PagedResponse<BookingDetailsDto>>.Failure(Error.Validation("Page size must be between 1 and 100."));
            if (userId == Guid.Empty)
            {
                return Result<PagedResponse<BookingDetailsDto>>.Failure(Error.Validation("A valid user ID is required."));
            }
            var bookings = await _bookingRepository.GetBookingsByUserIdAsync(userId, pageNumber, pageSize);
            return Result<PagedResponse<BookingDetailsDto>>.Success(bookings);
        }

        public async Task<Result<string>> CancelBooking(Guid bookingId, Guid userId)
        {
            if (bookingId == Guid.Empty)
            {
                return Result<string>.Failure(Error.Validation("A valid booking ID is required."));
            }

            if (userId == Guid.Empty)
            {
                return Result<string>.Failure(Error.Validation("A valid user ID is required."));
            }

            var booking = await _bookingRepository.GetBookingByIdAsync(bookingId);
            if (booking == null || booking.UserId != userId)
            {
                return Result<string>.Failure(Error.NotFound("Booking not found."));
            }

            if (booking.Status == BookingStatus.Confirmed)
            {
                return Result<string>.Failure(Error.Conflict("Confirmed bookings cannot be cancelled through this flow."));
            }
            //Idempotency should return ok when operation is retried on a cancelled booking not an error
            if (booking.Status == BookingStatus.Cancelled)
            {
                return Result<string>.Success("Booking is already cancelled");
            }

            if (booking.Status == BookingStatus.Expired)
            {
                return Result<string>.Failure(Error.Conflict("Expired bookings cannot be cancelled."));
            }

            if (booking.Status != BookingStatus.Pending)
            {
                return Result<string>.Failure(Error.Conflict($"Only pending bookings can be cancelled. Current status is {booking.Status}."));
            }

            var changed = await _bookingRepository.ChangeBookingStateAsync(
                bookingId, BookingStatus.Pending, BookingStatus.Cancelled);
            if (!changed)
            {
                // A concurrent payment or expiration beat us, seats were already released in the
                // line above, but the state didn't move. This is an inconsistency.
                _logger.LogError("Booking {BookingId} could not be transitioned to Cancelled",bookingId);
                return Result<string>.Failure(Error.Conflict("Booking status changed concurrently. Please refresh and try again."));
            }
            var releaseResult = await _catalogApi.ReleaseSeatsAsync(booking.EventId, booking.Quantity);
            if(!releaseResult.IsSuccess)
            {
                return Result<string>.Failure(Error.Failure("Failed to release seats."));
            }
            return Result<string>.Success("Booking cancelled successfully.");

        }
    }
}
