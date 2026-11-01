using EventHive.Shared.Common;
using EventHive.Shared.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Services;
using System.Security.Claims;

namespace NotificationService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetMyNotifications(int pageNumber = 1, int pageSize = 20)
        {
            var userIdStr = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized(Result<string>.Failure(
                    new List<Error> { Error.Unauthorized("Invalid or missing user ID in token.") }
                ));
            }

            var result = await _notificationService.GetUserNotificationsAsync(userId, pageNumber, pageSize);
            if (!result.IsSuccess)
            {
                return result.ToErrorResponse(this);
            }

            return Ok(result.Value);
        }

        [HttpGet("{id:guid}")]
        [Authorize]
        public async Task<IActionResult> GetNotificationById(Guid id)
        {
            var userIdStr = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized(Result<string>.Failure(
                    new List<Error> { Error.Unauthorized("Invalid or missing user ID in token.") }
                ));
            }

            var result = await _notificationService.GetNotificationByIdAsync(id, userId);
            if (!result.IsSuccess)
            {
                return result.ToErrorResponse(this);
            }

            return Ok(result.Value);
        }
    }
}
