using BookingService.Domain.Entities;
using BookingService.Domain.Enums;
using BookingService.Services.DTOs.Responses;
using EventHive.Shared.Common;

namespace BookingService.Infrastructure.Repositories
{
    public interface IBookingRepository
    {
        Task CreateBookingAsync(Booking newBooking);
        Task<bool> HasActivePendingBookingAsync(Guid userId, Guid eventId, CancellationToken cancellationToken = default);
        Task<Booking?> GetBookingByIdAsync(Guid bookingId);
        Task<PagedResponse<BookingDetailsDto>> GetBookingsByUserIdAsync(Guid userId, int pageNumber = 1, int pageSize = 20);
        Task UpdateBookingAsync(Booking booking);
        Task<bool> ChangeBookingStateAsync(Guid bookingId, BookingStatus expectedCurrent, BookingStatus newStatus);

    }
}
