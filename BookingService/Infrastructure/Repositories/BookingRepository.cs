using BookingService.Domain.Entities;
using BookingService.Domain.Enums;
using BookingService.Infrastructure.Persistence;
using BookingService.Services.DTOs.Responses;
using EventHive.Shared.Common;
using Microsoft.EntityFrameworkCore;

namespace BookingService.Infrastructure.Repositories
{
    public class BookingRepository : IBookingRepository
    {
        private readonly BookingDbContext _context;

        public BookingRepository(BookingDbContext context)
        {
            _context = context;
        }

        public async Task CreateBookingAsync(Booking newBooking)
        {
            await _context.Bookings.AddAsync(newBooking);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> HasActivePendingBookingAsync(Guid userId, Guid eventId, CancellationToken cancellationToken = default)
        {
            return await _context.Bookings
                .AnyAsync(b => b.UserId == userId && b.EventId == eventId && b.Status == BookingStatus.Pending, cancellationToken);
        }

        public async Task<Booking?> GetBookingByIdAsync(Guid bookingId)
        {
            return await _context.Bookings
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == bookingId);
        }

        public async Task<PagedResponse<BookingDetailsDto>> GetBookingsByUserIdAsync(Guid userId, int pageNumber = 1, int pageSize = 20)
        {
            var query = _context.Bookings.AsNoTracking().Where(b => b.UserId == userId);
            
            var totalCount = await query.CountAsync();

            var items = await query.OrderByDescending(b => b.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new BookingDetailsDto
                (
                    b.Id,
                    b.EventId,
                    b.UserId,
                    b.Quantity,
                    b.Status,
                    b.TotalPrice,
                    b.CreatedAt,
                    b.UpdatedAt,
                    null
                ))
                .ToListAsync();

            return new PagedResponse<BookingDetailsDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task UpdateBookingAsync(Booking booking)
        {
            _context.Bookings.Update(booking);
            await _context.SaveChangesAsync();
        }


        public async Task<bool> ChangeBookingStateAsync(
    Guid bookingId, BookingStatus expectedCurrent, BookingStatus newStatus)
        {
            var rows = await _context.Bookings
                .Where(b => b.Id == bookingId && b.Status == expectedCurrent)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(b => b.Status, newStatus)
                    .SetProperty(b => b.UpdatedAt, DateTime.UtcNow));

            return rows > 0;
        }

        public async Task<int> CancelEventBookings(Guid eventId)
        {
            var rows = await _context.Bookings.Where(b => b.EventId == eventId && b.Status != BookingStatus.Cancelled)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(b => b.Status, BookingStatus.Cancelled)
                    .SetProperty(b => b.UpdatedAt, DateTime.UtcNow));
            return rows;
        }

    }
}
