using BookingService.Services;
using BookingService.Services.DTOs.Requests;
using EventHive.Shared.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using EventHive.Shared.Extensions;

namespace BookingService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BookingController : ControllerBase
    {
        public readonly IBookingService _bookingService;
        public BookingController(IBookingService bookingService)
        {
            _bookingService = bookingService;
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateBooking(CreateBookingDto dto)
        {
            if (!ModelState.IsValid)
            {
                var validationErrors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => Error.Validation(e.ErrorMessage))
                    .ToList();

                return BadRequest(Result<string>.Failure(validationErrors));
            }
            var userId = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userId, out Guid parsedUserId))
            {
                return Unauthorized(Result<string>.Failure(
                    new List<Error> { Error.Unauthorized("Invalid or missing user ID in token.") }
                ));
            }
            var result = await _bookingService.CreateBooking(dto, parsedUserId);

            if (!result.IsSuccess)
            {
                return result.ToErrorResponse(this);
            }
            return Ok(result.Value);
        }

        [HttpGet("{bookingid:guid}")]
        [Authorize]
        public async Task<IActionResult> GetBookingById(Guid bookingid)
        {
            var userId = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userId, out Guid parsedUserId))
            {
                return Unauthorized(Result<string>.Failure(
                    new List<Error> { Error.Unauthorized("Invalid or missing user ID in token.") }
                ));
            }
            var result = await _bookingService.GetBookingById(bookingid, parsedUserId);
            if (!result.IsSuccess)
            {
                return result.ToErrorResponse(this);
            }
            return Ok(result.Value);
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetBookings(int pageNumber = 1, int pageSize = 20)
        {
            var userId = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userId, out Guid parsedUserId))
            {
                return Unauthorized(Result<string>.Failure(
                    new List<Error> { Error.Unauthorized("Invalid or missing user ID in token.") }
                ));
            }
            var result = await _bookingService.GetBookings(parsedUserId, pageNumber, pageSize);
            if (!result.IsSuccess)
            {
                return result.ToErrorResponse(this);
            }
            return Ok(result.Value);
        }

        [HttpPut("{bookingId:guid}/cancel")]
        [Authorize]
        public async Task<IActionResult> CancelBooking(Guid bookingId)
        {
            var userId = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userId, out Guid parsedUserId))
            {
                return Unauthorized(Result<string>.Failure(
                    new List<Error> { Error.Unauthorized("Invalid or missing user ID in token.") }
                ));
            }

            var result = await _bookingService.CancelBooking(bookingId, parsedUserId);
            if (!result.IsSuccess)
            {
                return result.ToErrorResponse(this);
            }

            return Ok(result.Value);
        }
    }
}
