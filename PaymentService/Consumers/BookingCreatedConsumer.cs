using EventHive.Shared.Common;
using EventHive.Shared.MessageBroker;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using PaymentService.Domain.Entities;
using PaymentService.Infrastructure.Repositories;
using PaymentService.Services.Requests;
using PaymentService.Services.Responses;
using Polly.CircuitBreaker;
using Polly.Timeout;
using System.Net.Http.Json;

namespace PaymentService.Consumers
{
    public class BookingCreatedConsumer : IConsumer<BookingCreated>
    {
        private readonly ILogger<BookingCreatedConsumer> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IPaymentRepository _paymentRepository;

        public BookingCreatedConsumer(
            ILogger<BookingCreatedConsumer> logger,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IPaymentRepository paymentRepository)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _paymentRepository = paymentRepository;
        }

        public async Task Consume(ConsumeContext<BookingCreated> context)
        {
            var message = context.Message;

            if (message.BookingId == Guid.Empty)
            {
                _logger.LogWarning("Received BookingCreated with empty BookingId. Discarding.");
                return;
            }

            // Idempotency
            var existing = await _paymentRepository.GetByBookingIdAsync(message.BookingId);
            if (existing != null)
            {
                _logger.LogInformation("Payment record already exists for booking {BookingId}. Duplicate message ignored.", message.BookingId);
                return;
            }

            if (!int.TryParse(_configuration["Paymob:IntegrationId"], out int integrationId))
            {
                _logger.LogError("Paymob:IntegrationId is not configured or is not a valid integer.");
                throw new InvalidOperationException("Paymob:IntegrationId is missing or invalid.");
            }

            var payload = new PaymentRequestDto
            {
                amount = message.AmountDue*100,
                currency = "EGP",
                payment_methods = new List<int> { integrationId },
                items = new List<PaymentItem>
                {
                    new PaymentItem
                    {
                        name = message.EventName ?? "Event Ticket",
                        quantity = message.Quantity,
                        amount = message.UnitPrice*100
                    }
                },
                redirection_url = _configuration["Paymob:RedirectionUrl"] ?? string.Empty,
                billing_data = new BillingData
                {
                    first_name = "NA",
                    last_name = "NA",
                    email = "fadynagytest@gmail.com",
                    phone_number = "+201000000000",
                    apartment = "NA",
                    floor = "NA",
                    street = "NA",
                    building = "NA",
                    shipping_method = "NA",
                    postal_code = "NA",
                    city = "NA",
                    country = "NA",
                    state = "NA"
                },
                extras = new PaymentExtras
                {
                    booking_id = message.BookingId.ToString()
                }
            };

            var client = _httpClientFactory.CreateClient("Paymob");
            HttpResponseMessage response;

            try
            {
                response = await client.PostAsJsonAsync("/v1/intention/", payload);
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogWarning(ex, "Paymob circuit breaker is OPEN for booking {BookingId}.", message.BookingId);
                await HandlePaymentFailureAsync(message, context, ServiceMessages.PaymentGatewayUnavailable);
                return;
            }
            catch (TimeoutRejectedException ex)
            {
                _logger.LogWarning(ex, "Paymob call timed out for booking {BookingId}.", message.BookingId);
                await HandlePaymentFailureAsync(message, context, ServiceMessages.PaymentGatewayUnavailable);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HTTP call to Paymob /v1/intention failed for booking {BookingId}.", message.BookingId);
                // Re-throw so MassTransit retries the message
                throw;
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Paymob returned {StatusCode} for booking {BookingId}. Body: {Body}",
                    response.StatusCode, message.BookingId, errorBody);
                // Re-throw so MassTransit retries
                throw new Exception($"Paymob intention creation failed with status {response.StatusCode}.");
            }

            var intentionResponse = await response.Content.ReadFromJsonAsync<PaymobIntentionResponse>();

            if (intentionResponse?.client_secret == null || intentionResponse.intention_order_id == null)
            {
                _logger.LogError("Paymob response for booking {BookingId} was missing client_secret or intention_order_id.", message.BookingId);
                throw new Exception("Paymob response missing required fields.");
            }

            var publicKey = _configuration["Paymob:PublicKey"];
            var baseUrl = _configuration["Paymob:BaseUrl"]?.TrimEnd('/') ?? "https://accept.paymob.com";
            var checkoutUrl = !string.IsNullOrWhiteSpace(publicKey)
                ? $"{baseUrl}/unifiedcheckout/?publicKey={publicKey}&clientSecret={intentionResponse.client_secret}"
                : null;

            if (string.IsNullOrWhiteSpace(publicKey))
            {
                _logger.LogWarning("Paymob:PublicKey is not configured. Unified Checkout URL could not be constructed.");
            }

            var payment = new Payment
            {
                BookingId = message.BookingId,
                UserId = message.UserId,
                Amount = message.AmountDue,
                ExternalOrderId = intentionResponse.intention_order_id.ToString(),
                Status = "Pending",
                ClientSecret = intentionResponse.client_secret,
                CheckoutUrl = checkoutUrl
            };

            try
            {
                await _paymentRepository.CreateAsync(payment);
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UQ_Payments_BookingId") == true)
            {
                _logger.LogWarning("Duplicate payment insert for booking {BookingId} caught by unique index. Ignoring.", message.BookingId);
                return;
            }

            _logger.LogInformation(
                "Payment record created for booking {BookingId}. ExternalOrderId: {ExternalOrderId}.",
                message.BookingId, intentionResponse.intention_order_id);
        }

        private async Task HandlePaymentFailureAsync(BookingCreated message, ConsumeContext<BookingCreated> context, string reason)
        {
            var payment = new Payment
            {
                BookingId = message.BookingId,
                UserId = message.UserId,
                Amount = message.AmountDue,
                Status = "Failed"
            };

            try
            {
                await _paymentRepository.CreateAsync(payment);
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UQ_Payments_BookingId") == true)
            {
                _logger.LogWarning("Duplicate payment insert for booking {BookingId} caught by unique index.", message.BookingId);
            }

            await context.Publish(new PaymentFailed(message.BookingId, reason),
                ctx => ctx.SetRoutingKey("payment.failed"));
        }
    }
}
