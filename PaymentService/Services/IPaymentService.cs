using EventHive.Shared.Common;
using PaymentService.Services.Responses;

namespace PaymentService.Services
{
    public interface IPaymentService
    {
        Task<Result<string>> ProcessPaymobWebhook(string? hmac, PaymobWebhookPayload? payload);
        Task<Result<PaymentDetailsDto>> GetPaymentByBookingId(Guid bookingId, Guid userId);
    }
}
