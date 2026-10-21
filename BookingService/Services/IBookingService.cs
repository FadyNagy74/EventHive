using BookingService.Services.DTOs.Requests;
using BookingService.Services.DTOs.Responses;
using EventHive.Shared.Common;

namespace BookingService.Services
{
    public interface IBookingService
    {
        Task<Result<BookingResponseDto>> CreateBooking(CreateBookingDto dto, Guid userId);
        Task<Result<BookingDetailsDto>> GetBookingById(Guid bookingId, Guid userId);
        Task<Result<PagedResponse<BookingDetailsDto>>> GetBookings(Guid userId, int pageNumber = 1, int pageSize = 20);
        Task<Result<string>> CancelBooking(Guid bookingId, Guid userId);
    }
}
