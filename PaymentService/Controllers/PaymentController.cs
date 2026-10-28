using EventHive.Shared.Common;
using EventHive.Shared.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Services;
using PaymentService.Services.Responses;
using System.Security.Claims;

namespace PaymentService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        public readonly IPaymentService _paymentService;

        public PaymentController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        // Security is enforced via HMAC in the service, not JWT
        [HttpPost("~/webhook/payment")]
        [AllowAnonymous]
        public async Task<IActionResult> HandlePaymobWebhook(
            string hmac,
            PaymobWebhookPayload payload)
        {
            if (!ModelState.IsValid)
            {
                var validationErrors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => Error.Validation(e.ErrorMessage))
                    .ToList();

                return BadRequest(Result<string>.Failure(validationErrors));
            }

            var result = await _paymentService.ProcessPaymobWebhook(hmac, payload);
            if (!result.IsSuccess)
            {
                return result.ToErrorResponse(this);
            }

            return Ok(result.Value);
        }

        [HttpGet("booking/{bookingId:guid}")]
        [Authorize]
        public async Task<IActionResult> GetPaymentByBookingId(Guid bookingId)
        {
            var userIdStr = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(userIdStr, out Guid userId))
            {
                return Unauthorized(Result<string>.Failure(
                    new List<Error> { Error.Unauthorized("Invalid or missing user ID in token.") }
                ));
            }

            var result = await _paymentService.GetPaymentByBookingId(bookingId, userId);
            if (!result.IsSuccess)
            {
                return result.ToErrorResponse(this);
            }

            return Ok(result.Value);
        }
    }
}
