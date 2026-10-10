using CatalogService.Domain.Entities;
using CatalogService.DTOs;
using CatalogService.Infrastructure.Persistence;
using EventHive.Shared.Common;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Domain.Repositories
{
    public class EventRepository : IEventRepository
    {
        private readonly CatalogDbContext _context;

        public EventRepository(CatalogDbContext context)
        {
            _context = context;
        }

        public async Task<PagedResponse<PublishedEventDto>> GetPublishedEvents(int pageNumber, int pageSize)
        {
            var query = _context.Events.AsNoTracking()
                .Where(e => e.Status == Enums.EventStatus.Published);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(e => e.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new PublishedEventDto(
                    e.Name,
                    e.Description,
                    e.Date,
                    e.Venue,
                    e.Price,
                    e.RemainingSeats
                ))
                .ToListAsync();

            return new PagedResponse<PublishedEventDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<PublishedEventDto?> GetPublishedEventById(Guid Id)
        {

            return await _context.Events.AsNoTracking().Where(e => e.Id == Id && (e.Status == Enums.EventStatus.Published || e.Status == Enums.EventStatus.Closed))
                .Select(e => new PublishedEventDto(
                    e.Name,
                    e.Description,
                    e.Date,
                    e.Venue,
                    e.Price,
                    e.RemainingSeats
                ))
                .FirstOrDefaultAsync();

        }

        public async Task<bool> ReserveSeatsAsync(Guid eventId, int quantity)
        {
            var ev = await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId && e.Status == Enums.EventStatus.Published);
            
            if (ev == null || ev.RemainingSeats < quantity)
            {
                return false;
            }

            ev.RemainingSeats -= quantity;
            _context.Events.Update(ev);
            var rows = await _context.SaveChangesAsync();
            
            return rows > 0;
        }

        public async Task<bool> ReleaseSeatsAsync(Guid eventId, int quantity)
        {
            var ev = await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId);
            
            if (ev == null)
            {
                return false;
            }

            ev.RemainingSeats += quantity;
            _context.Events.Update(ev);
            var rows = await _context.SaveChangesAsync();
            
            return rows > 0;
        }

        public async Task CreateEventAsync(Event newEvent)
        {
            await _context.Events.AddAsync(newEvent);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> IsEventDuplicateAsync(CreateEventDto dto)
        {
            return await _context.Events.AnyAsync(e =>
    e.Name == dto.Name &&
    e.Venue == dto.Venue &&
    e.Date == dto.Date.Value);
        }


        public async Task<Event?> GetEventByIdAsync(Guid id)
        {
            return await _context.Events.FirstOrDefaultAsync(e => e.Id == id);
        }

        public async Task UpdateEventAsync(Event ev)
        {
            _context.Events.Update(ev);
            await _context.SaveChangesAsync();
        }

        public async Task<Event?> GetEventWithAdjustmentsByIdAsync(Guid id)
        {
            return await _context.Events.Include(e => e.CapacityAdjustments).FirstOrDefaultAsync(e => e.Id == id);
        }

        public async Task AddManualAdjustmentForEvent(ManualCapacityAdjustment adjustment)
        {
            _context.ManualCapacityAdjustments.Add(adjustment);
            await _context.SaveChangesAsync();
        }
    }
}
