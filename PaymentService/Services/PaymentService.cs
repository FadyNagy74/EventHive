using EventHive.Shared.Common;
using EventHive.Shared.MessageBroker;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using PaymentService.Domain.Entities;
using PaymentService.Infrastructure.Repositories;
using PaymentService.Services.Responses;
using System.Security.Cryptography;
using System.Text;

namespace PaymentService.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(
            IPaymentRepository paymentRepository,
            IPublishEndpoint publishEndpoint,
            IConfiguration configuration,
            ILogger<PaymentService> logger)
        {
            _paymentRepository = paymentRepository;
            _publishEndpoint = publishEndpoint;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<Result<string>> ProcessPaymobWebhook(string? hmac, PaymobWebhookPayload? payload)
        {
            if (payload?.obj == null || payload.type != "TRANSACTION")
            {
                _logger.LogWarning("Received non-TRANSACTION webhook type: {Type}. Safely ignoring.", payload?.type);
                return Result<string>.Success("Non-transaction webhook ignored.");
            }

            if (string.IsNullOrWhiteSpace(hmac))
            {
                _logger.LogWarning("Paymob webhook received without HMAC signature.");
                return Result<string>.Failure(Error.Unauthorized("HMAC signature is required."));
            }

            if (!VerifyHmac(payload.obj, hmac))
            {
                _logger.LogWarning("Paymob webhook HMAC verification failed. Potential spoofing attempt.");
                return Result<string>.Failure(Error.Unauthorized("HMAC verification failed."));
            }

            var transaction = payload.obj;
            var externalTransactionId = transaction.id.ToString();
            var externalOrderId = transaction.order?.id.ToString();

            if (string.IsNullOrWhiteSpace(externalOrderId))
            {
                _logger.LogWarning("Webhook transaction {TransactionId} has no external order id. Safely ignoring.", externalTransactionId);
                return Result<string>.Success("Webhook ignored: missing order id.");
            }

            // Check if transaction is already recorded (Idempotency)
            var isAlreadyRecorded = await _paymentRepository.TransactionExistsAsync(externalTransactionId);
            if (isAlreadyRecorded)
            {
                _logger.LogInformation("Duplicate webhook for transaction {TransactionId}. Safely ignoring.", externalTransactionId);
                return Result<string>.Success("Duplicate transaction ignored.");
            }

            var payment = await _paymentRepository.GetByExternalOrderIdAsync(externalOrderId);
            if (payment == null)
            {
                _logger.LogWarning(
                    "Webhook received for unknown Paymob order {ExternalOrderId}. Transaction {TransactionId}. Safely ignoring.",
                    externalOrderId, externalTransactionId);
                return Result<string>.Success("Payment not found for external order id; ignored.");
            }

            var txRecord = new Transaction
            {
                PaymentId = payment.Id,
                ExternalTransactionId = externalTransactionId,
                Status = transaction.success ? "Success" : "Failed"
            };

            try
            {
                await _paymentRepository.AddTransactionAsync(txRecord);
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UQ_Transactions_ExternalTransactionId") == true)
            {
                _logger.LogWarning("Concurrent duplicate webhook for transaction {TransactionId} caught by unique constraint.", externalTransactionId);
                return Result<string>.Success("Duplicate transaction ignored.");
            }

            if (payment.Status != "Pending")
            {
                _logger.LogInformation(
                    "Payment {PaymentId} is already in {Status} state. Transaction {TransactionId} recorded but status unchanged.",
                    payment.Id, payment.Status, externalTransactionId);
                return Result<string>.Success($"Payment was already in {payment.Status} state.");
            }

            if (transaction.success)
            {
                payment.Status = "Succeeded";
                payment.UpdatedAt = DateTime.UtcNow;
                await _paymentRepository.UpdateAsync(payment);

                await _publishEndpoint.Publish(new PaymentSucceeded(payment.BookingId),
                    context => context.SetRoutingKey("payment.succeeded"));

                _logger.LogInformation(
                    "Payment succeeded for booking {BookingId}. Transaction {TransactionId}.",
                    payment.BookingId, externalTransactionId);

                return Result<string>.Success("Payment processed successfully.");
            }
            else
            {
                payment.Status = "Failed";
                payment.UpdatedAt = DateTime.UtcNow;
                await _paymentRepository.UpdateAsync(payment);

                var reason = transaction.error_occured
                    ? "Payment error occurred."
                    : "Payment was declined or unsuccessful.";

                await _publishEndpoint.Publish(new PaymentFailed(payment.BookingId, reason),
                    context => context.SetRoutingKey("payment.failed"));

                _logger.LogWarning(
                    "Payment failed for booking {BookingId}. Transaction {TransactionId}. Reason: {Reason}",
                    payment.BookingId, externalTransactionId, reason);

                return Result<string>.Success("Payment failure recorded.");
            }
        }

        public async Task<Result<PaymentDetailsDto>> GetPaymentByBookingId(Guid bookingId, Guid userId)
        {
            if (bookingId == Guid.Empty)
            {
                return Result<PaymentDetailsDto>.Failure(Error.Validation("A valid Booking ID is required."));
            }

            if (userId == Guid.Empty)
            {
                return Result<PaymentDetailsDto>.Failure(Error.Unauthorized("A valid User ID is required."));
            }

            var payment = await _paymentRepository.GetByBookingIdAsync(bookingId);
            if (payment == null)
            {
                return Result<PaymentDetailsDto>.Failure(Error.NotFound("Payment not found for the specified booking."));
            }

            if (payment.UserId != Guid.Empty && payment.UserId != userId)
            {
                return Result<PaymentDetailsDto>.Failure(Error.Unauthorized("You are not authorized to view the payment for this booking."));
            }

            // Fallback construction of CheckoutUrl if not already saved in database
            var checkoutUrl = payment.CheckoutUrl;
            if (string.IsNullOrWhiteSpace(checkoutUrl) && !string.IsNullOrWhiteSpace(payment.ClientSecret))
            {
                var publicKey = _configuration["Paymob:PublicKey"];
                if (!string.IsNullOrWhiteSpace(publicKey))
                {
                    var baseUrl = _configuration["Paymob:BaseUrl"]?.TrimEnd('/') ?? "https://accept.paymob.com";
                    checkoutUrl = $"{baseUrl}/unifiedcheckout/?publicKey={publicKey}&clientSecret={payment.ClientSecret}";
                }
            }

            var dto = new PaymentDetailsDto(
                payment.Id,
                payment.BookingId,
                payment.UserId,
                payment.Status,
                payment.Amount,
                payment.ClientSecret,
                checkoutUrl,
                payment.CreatedAt,
                payment.UpdatedAt
            );

            return Result<PaymentDetailsDto>.Success(dto);
        }

        private bool VerifyHmac(PaymobTransactionObj obj, string receivedHmac)
        {
            var hmacSecret = _configuration["Paymob:HmacSecret"];
            if (string.IsNullOrWhiteSpace(hmacSecret))
            {
                _logger.LogError("Paymob:HmacSecret is not configured. HMAC verification cannot proceed.");
                return false;
            }

            var concatenated = string.Concat(
                obj.amount_cents,
                obj.created_at,
                obj.currency,
                obj.error_occured.ToString().ToLower(),
                obj.has_parent_transaction.ToString().ToLower(),
                obj.id,
                obj.integration_id,
                obj.is_3d_secure.ToString().ToLower(),
                obj.is_auth.ToString().ToLower(),
                obj.is_capture.ToString().ToLower(),
                obj.is_refunded.ToString().ToLower(),
                obj.is_standalone_payment.ToString().ToLower(),
                obj.is_voided.ToString().ToLower(),
                obj.order?.id,
                obj.owner,
                obj.pending.ToString().ToLower(),
                obj.source_data?.pan,
                obj.source_data?.sub_type,
                obj.source_data?.type,
                obj.success.ToString().ToLower()
            );

            using var hmacSha512 = new HMACSHA512(Encoding.UTF8.GetBytes(hmacSecret));
            var computedBytes = hmacSha512.ComputeHash(Encoding.UTF8.GetBytes(concatenated));
            var computedHmac = Convert.ToHexString(computedBytes).ToLower();

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedHmac),
                Encoding.UTF8.GetBytes(receivedHmac.Trim().ToLowerInvariant()));
        }
    }
}
