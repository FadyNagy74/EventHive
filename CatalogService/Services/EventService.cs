using CatalogService.Domain.Entities;
using CatalogService.Domain.Enums;
using CatalogService.Infrastructure.Repositories;
using CatalogService.Services.DTOs.Requests;
using CatalogService.Services.DTOs.Responses;
using EventHive.Shared.Common;
using EventHive.Shared.MessageBroker;
using MassTransit;
using Microsoft.IdentityModel.Tokens;

namespace CatalogService.Services
{
    public class EventService : IEventService
    {
        private readonly IEventRepository _eventRepository;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<EventService> _logger;

        public EventService(
            IEventRepository eventRepository,
            IPublishEndpoint publishEndpoint,
            ILogger<EventService> logger)
        {
            _eventRepository  = eventRepository;
            _publishEndpoint  = publishEndpoint;
            _logger           = logger;
        }

        public async Task<Result<PagedResponse<PublishedEventDto>>> GetPublishedEventsAsync(int pageNumber = 1, int pageSize = 20) {
            if (pageNumber <= 0) return Result<PagedResponse<PublishedEventDto>>.Failure(Error.Validation("Page number must be greater than 0."));
            if (pageSize <= 0 || pageSize > 100) return Result<PagedResponse<PublishedEventDto>>.Failure(Error.Validation("Page size must be between 1 and 100."));

            var pagedResponse = await _eventRepository.GetPublishedEvents(pageNumber, pageSize);
            return Result<PagedResponse<PublishedEventDto>>.Success(pagedResponse);
        }
        public async Task<Result<PublishedEventDto>> GetPublishedEventByIdAsync(Guid id)
        {
            PublishedEventDto? publishedEvent = await _eventRepository.GetPublishedEventById(id);
            
            if (publishedEvent == null)
            {
                return Result<PublishedEventDto>.Failure(Error.NotFound("Event not found"));
            }

            return Result<PublishedEventDto>.Success(publishedEvent);
        }


        public async Task<Result<ReserveSeatsResponseDto>> ReserveSeatsAsync(Guid id, string eventName, int quantity)
        {
            if (quantity <= 0)
                return Result<ReserveSeatsResponseDto>.Failure(Error.Validation("Quantity must be greater than zero."));

            var (success, unitPrice, eventDate) = await _eventRepository.ReserveSeatsAsync(id, eventName, quantity);
            
            if (!success)
            {
                return Result<ReserveSeatsResponseDto>.Failure(Error.Conflict("Failed to reserve seats. Either the event does not exist, is not published, event name doesn't match, or there are not enough remaining seats."));
            }

            var totalPrice = unitPrice * quantity;
            return Result<ReserveSeatsResponseDto>.Success(new ReserveSeatsResponseDto(unitPrice, totalPrice, eventDate));
        }

        public async Task<Result<string>> ReleaseSeatsAsync(Guid id, int quantity)
        {
            if (quantity <= 0)
                return Result<string>.Failure(Error.Validation("Quantity must be greater than zero."));

            var success = await _eventRepository.ReleaseSeatsAsync(id, quantity);

            if (!success)
            {
                return Result<string>.Failure(Error.NotFound("Failed to release seats. Event not found."));
            }

            return Result<string>.Success("Seats released successfully");
        }

        public async Task<Result<string>> CreateEvent(CreateEventDto dto)
        {
            if(dto.TotalCapacity < 0)
            {
                return Result<string>.Failure(Error.Validation("Total capacity must be a positive integer"));
            }
            if (dto.Date <= DateTime.UtcNow)
            {
                return Result<string>.Failure(Error.Validation("The event date must be in the future."));
            }
            if (dto.Price < 0)
            {
                return Result<string>.Failure(Error.Validation("Price cannot be negative."));
            }

            bool isDuplicate = await _eventRepository.IsEventDuplicateAsync(dto);
            if(isDuplicate)
            {
                return Result<string>.Failure(Error.Validation("Event must not have the same name, venue and date."));
            }

            Domain.Entities.Event newEvent = new Domain.Entities.Event
            {
                Name = dto.Name,
                Description = dto.Description,
                Date = dto.Date.Value,
                Venue = dto.Venue,
                Price = dto.Price.Value,
                TotalCapacity = dto.TotalCapacity.Value,
                RemainingSeats = dto.RemainingSeats.Value,
                Status = EventStatus.Draft,
            };

            await _eventRepository.CreateEventAsync(newEvent);

            return Result<string>.Success(newEvent.Id.ToString());
        }

        public async Task<Result<string>> UpdateEventAsync(Guid id, UpdateEventDto dto)
        {
            if (dto.TotalCapacity < 0)
                return Result<string>.Failure(Error.Validation("Total capacity must be a positive integer."));

            if (dto.Price < 0)
                return Result<string>.Failure(Error.Validation("Price cannot be negative."));


            var ev = await _eventRepository.GetEventByIdAsync(id);
            if (ev == null)
                return Result<string>.Failure(Error.NotFound("Event not found."));

            var takenSeats = ev.TotalCapacity - ev.RemainingSeats;

            if (dto.TotalCapacity < takenSeats)
            {
                return Result<string>.Failure(Error.Conflict($"Cannot decrease capacity to {dto.TotalCapacity} because {takenSeats} seats are already reserved or sold."));
            }

            ev.Name = dto.Name;
            ev.Description = dto.Description;
            ev.Date = dto.Date.Value;
            ev.Venue = dto.Venue;
            ev.Price = dto.Price.Value;
            
            ev.RemainingSeats += (dto.TotalCapacity.Value - ev.TotalCapacity);
            ev.TotalCapacity = dto.TotalCapacity.Value;

            await _eventRepository.UpdateEventAsync(ev);

            return Result<string>.Success("Event updated successfully");
        }

        public async Task<Result<string>> PublishEventAsync(Guid id)
        {
            var ev = await _eventRepository.GetEventByIdAsync(id);
            if (ev == null)
                return Result<string>.Failure(Error.NotFound("Event not found."));

            if (ev.Status == EventStatus.Published)
                return Result<string>.Success("Event is already published");

            if (ev.Status == EventStatus.Closed)
                return Result<string>.Failure(Error.Validation("A closed event cannot be republished."));

            if (ev.Status == EventStatus.Cancelled)
                return Result<string>.Failure(Error.Validation("A cancelled event cannot be republished."));

            if (ev.Date <= DateTime.UtcNow)
                return Result<string>.Failure(Error.Validation("Cannot publish an event that is in the past."));

            ev.Status = EventStatus.Published;
            await _eventRepository.UpdateEventAsync(ev);

            return Result<string>.Success("Event published successfully");
        }

        public async Task<Result<string>> CloseEventAsync(Guid id)
        {
            var ev = await _eventRepository.GetEventByIdAsync(id);
            if (ev == null)
                return Result<string>.Failure(Error.NotFound("Event not found."));

            if (ev.Status == EventStatus.Closed)
                return Result<string>.Success("Event is already closed");

            if (ev.Status != EventStatus.Published)
                return Result<string>.Failure(Error.Validation("Only published events can be closed."));

            ev.Status = EventStatus.Closed;
            await _eventRepository.UpdateEventAsync(ev);

            return Result<string>.Success("Event closed successfully");
        }

        public async Task<Result<string>> CancelEventAsync(Guid id)
        {
            var ev = await _eventRepository.GetEventByIdAsync(id);
            if (ev == null)
                return Result<string>.Failure(Error.NotFound("Event not found."));

            if (ev.Status == EventStatus.Cancelled)
                return Result<string>.Success("Event is already cancelled");

            if (ev.Date <= DateTime.UtcNow)
                return Result<string>.Failure(Error.Validation("Cannot cancel an event that has already occurred."));

            ev.Status = EventStatus.Cancelled;
            await _eventRepository.UpdateEventAsync(ev);

            //RabbitMq
            await _publishEndpoint.Publish(new EventCancelled(ev.Id), context => context.SetRoutingKey("event.cancelled"));
            _logger.LogInformation("EventCancelled published for event {EventId}.", ev.Id);

            return Result<string>.Success("Event cancelled successfully");
        }

        public async Task<Result<string>> AdjustRemainingSeatsAsync(Guid id, AdjustCapacityDto dto, string userId, string name)
        {
            if (dto.RemainingSeats < 0)
            {
                return Result<string>.Failure(Error.Validation("Seat count cannot be less than 0."));
            }

            var ev = await _eventRepository.GetEventWithAdjustmentsByIdAsync(id);
            if (ev == null)
                return Result<string>.Failure(Error.NotFound("Event not found."));

            if (dto.RemainingSeats > ev.TotalCapacity)
            {
                return Result<string>.Failure(Error.Validation("Capacity doesn't match available seats. Cannot set remaining seats higher than total capacity."));
            }

            var oldRemainingSeats = ev.RemainingSeats;
            
            if (oldRemainingSeats == dto.RemainingSeats)
            {
                return Result<string>.Success("No changes were made to the seat count.");
            }

            ev.RemainingSeats = dto.RemainingSeats.Value;

            ManualCapacityAdjustment adjustment = new ManualCapacityAdjustment
            {
                EventId = ev.Id,
                ChangeDelta = dto.RemainingSeats.Value - oldRemainingSeats,
                Reason = dto.Reason,
                AdjustedBy = name,
                AdjustedById = Guid.Parse(userId),
            };

            await _eventRepository.AddManualAdjustmentForEvent(adjustment);
            
            return Result<string>.Success("Remaining seats adjusted successfully");
        }
    }
}
