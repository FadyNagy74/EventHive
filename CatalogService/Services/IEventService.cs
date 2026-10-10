using CatalogService.DTOs;
using EventHive.Shared.Common;

namespace CatalogService.Services
{
    public interface IEventService
    {
        Task<Result<PagedResponse<PublishedEventDto>>> GetPublishedEventsAsync(int pageNumber = 1, int pageSize = 20);
        Task<Result<PublishedEventDto>> GetPublishedEventByIdAsync(Guid id);
        Task<Result<EventAvailabilityDto>> AvailableSeats(Guid id);
        Task<Result<string>> ReserveSeatsAsync(Guid id, int quantity);
        Task<Result<string>> ReleaseSeatsAsync(Guid id, int quantity);
        Task<Result<string>> CreateEvent(CreateEventDto dto);
        Task<Result<string>> UpdateEventAsync(Guid id, UpdateEventDto dto);
        Task<Result<string>> PublishEventAsync(Guid id);
        Task<Result<string>> CloseEventAsync(Guid id);
        Task<Result<string>> CancelEventAsync(Guid id);
        Task<Result<string>> AdjustRemainingSeatsAsync(Guid id, AdjustCapacityDto dto, string userId, string name);
    }
}