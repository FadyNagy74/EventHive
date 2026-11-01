using CatalogService.Domain.Entities;
using CatalogService.Services.DTOs.Requests;
using CatalogService.Services.DTOs.Responses;
using EventHive.Shared.Common;

namespace CatalogService.Infrastructure.Repositories
{
    public interface IEventRepository
    {
        public Task<PagedResponse<PublishedEventDto>> GetPublishedEvents(int pageNumber, int pageSize);
        public Task<PublishedEventDto?> GetPublishedEventById(Guid Id);
        Task<(bool Success, decimal UnitPrice, DateTime EventDate)> ReserveSeatsAsync(Guid eventId, string eventName, int quantity);
        Task<bool> ReleaseSeatsAsync(Guid eventId, int quantity);
        Task CreateEventAsync(Event newEvent);
        Task<Event?> GetEventByIdAsync(Guid id);
        Task UpdateEventAsync(Event ev);
        Task<Event?> GetEventWithAdjustmentsByIdAsync(Guid id);
        Task AddManualAdjustmentForEvent(ManualCapacityAdjustment adjustment);
        Task<bool> IsEventDuplicateAsync(CreateEventDto dto);
    }
}
