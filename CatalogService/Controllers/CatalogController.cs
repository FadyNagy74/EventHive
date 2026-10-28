using Azure.Core;
using CatalogService.Services.DTOs.Requests;
using CatalogService.Services.DTOs.Responses;
using CatalogService.Services;
using EventHive.Shared.Common;
using EventHive.Shared.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace CatalogService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CatalogController : ControllerBase
    {
        private readonly IEventService _eventService;

        public CatalogController(IEventService eventService)
        {
            _eventService = eventService;
        }

        [HttpGet("events")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublishedEvents(int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            var result = await _eventService.GetPublishedEventsAsync(pageNumber, pageSize);

            if (!result.IsSuccess)
                return result.ToErrorResponse(this);

            return Ok(result.Value);
        }
        [HttpGet("event/{id:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublishedEvent(Guid id)
        {
            var result = await _eventService.GetPublishedEventByIdAsync(id);
            if (!result.IsSuccess)
            {
                return result.ToErrorResponse(this);
            }
            return Ok(result.Value);
        }

        [HttpPost("~/internal/catalog/event/{id:guid}/reserve")]
        [Authorize]
        public async Task<IActionResult> ReserveSeats(Guid id, string eventName, int quantity)
        {
            var result = await _eventService.ReserveSeatsAsync(id, eventName, quantity);
            if (!result.IsSuccess)
            {
                return result.ToErrorResponse(this);
            }
            return Ok(result.Value);
        }

        [HttpPost("~/internal/catalog/event/{id:guid}/release")]
        [Authorize]
        public async Task<IActionResult> ReleaseSeats(Guid id, int quantity)
        {
            var result = await _eventService.ReleaseSeatsAsync(id, quantity);
            if (!result.IsSuccess)
            {
                return result.ToErrorResponse(this);
            }
            return Ok(result.Value);
        }

        [HttpPost("event")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateEvent(CreateEventDto dto)
        {

            if (!ModelState.IsValid)
            {
                var validationErrors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => Error.Validation(e.ErrorMessage))
                    .ToList();

                return BadRequest(Result<string>.Failure(validationErrors));
            }

            var result = await _eventService.CreateEvent(dto);
            
            if (!result.IsSuccess)
            {
                return result.ToErrorResponse(this);
            }
            return Ok(result.Value);
        }

        [HttpPut("event/{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateEvent(Guid id, UpdateEventDto request)
        {
            if (!ModelState.IsValid)
            {
                var validationErrors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => Error.Validation(e.ErrorMessage))
                    .ToList();

                return BadRequest(Result<string>.Failure(validationErrors));
            }

            var result = await _eventService.UpdateEventAsync(id, request);
            if (!result.IsSuccess)
            {
                return result.ToErrorResponse(this);
            }
            return Ok(result.Value);
        }

        [HttpPut("event/publish/{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PublishEvent(Guid id)
        {
            var result = await _eventService.PublishEventAsync(id);
            if (!result.IsSuccess)
            {
                return result.ToErrorResponse(this);
            }
            return Ok(result.Value);
        }

        [HttpPut("event/close/{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CloseEvent(Guid id)
        {
            var result = await _eventService.CloseEventAsync(id);
            if (!result.IsSuccess)
            {
                return result.ToErrorResponse(this);
            }
            return Ok(result.Value);
        }

        [HttpPut("event/cancel/{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CancelEvent(Guid id)
        {
            var result = await _eventService.CancelEventAsync(id);
            if (!result.IsSuccess)
            {
                return result.ToErrorResponse(this);
            }
            return Ok(result.Value);
        }

        [HttpPut("event/adjust-remaining-seats/{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdjustRemainingSeats(Guid id, AdjustCapacityDto dto)
        {
            if (!ModelState.IsValid)
            {
                var validationErrors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => Error.Validation(e.ErrorMessage))
                    .ToList();

                return BadRequest(Result<string>.Failure(validationErrors));
            }
            var userId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var name = HttpContext.User.FindFirst(ClaimTypes.Name)?.Value;
            var result = await _eventService.AdjustRemainingSeatsAsync(id, dto, userId, name);
            if (!result.IsSuccess)
            {
                return result.ToErrorResponse(this);
            }
            return Ok(result.Value);
        }
    }
}
