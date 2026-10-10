using CatalogService.Domain.Entities;
using CatalogService.DTOs;
using EventHive.Shared.Common;

namespace CatalogService.Domain.Repositories
{
    public interface IEventRepository
    {
        public Task<PagedResponse<PublishedEventDto>> GetPublishedEvents(int pageNumber, int pageSize);
        public Task<PublishedEventDto?> GetPublishedEventById(Guid Id);
        Task<bool> ReserveSeatsAsync(Guid eventId, int quantity);
        Task<bool> ReleaseSeatsAsync(Guid eventId, int quantity);
        Task CreateEventAsync(Event newEvent);
        Task<Event?> GetEventByIdAsync(Guid id);
        Task UpdateEventAsync(Event ev);
        Task<Event?> GetEventWithAdjustmentsByIdAsync(Guid id);
        Task AddManualAdjustmentForEvent(ManualCapacityAdjustment adjustment);
        Task<bool> IsEventDuplicateAsync(CreateEventDto dto);
    }
}
