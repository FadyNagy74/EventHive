using EventHive.Shared.Common;
using NotificationService.Domain.Entities;
using NotificationService.Infrastructure.Repositories;
using NotificationService.Infrastructure.Senders;
using NotificationService.Services.DTOs.Responses;

namespace NotificationService.Services
{
    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly INotificationSender _notificationSender;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            INotificationRepository notificationRepository,
            INotificationSender notificationSender,
            ILogger<NotificationService> logger)
        {
            _notificationRepository = notificationRepository;
            _notificationSender = notificationSender;
            _logger = logger;
        }

        public async Task<Result<NotificationResponseDto>> CreatePendingBookingNotificationAsync(
            Guid bookingId,
            Guid userId,
            Guid eventId,
            string eventName,
            DateTime eventDate,
            int quantity,
            Guid? messageId,
            CancellationToken cancellationToken = default)
        {
            if (bookingId == Guid.Empty)
            {
                return Result<NotificationResponseDto>.Failure(Error.Validation("BookingId cannot be empty."));
            }

            var existing = await _notificationRepository.GetByBookingIdAsync(bookingId, cancellationToken);
            if (existing != null)
            {
                _logger.LogInformation("Notification record for BookingId {BookingId} already exists. Skipping creation.", bookingId);
                return Result<NotificationResponseDto>.Success(MapToDto(existing));
            }

            var dateString = eventDate != default ? eventDate.ToString("MMMM dd, yyyy") : "scheduled date";
            var ticketUnit = quantity == 1 ? "ticket" : "tickets";
            var message = $"Your booking for '{eventName}' ({quantity} {ticketUnit}) on {dateString} has been confirmed!";

            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                BookingId = bookingId,
                UserId = userId,
                EventId = eventId,
                EventName = eventName,
                Type = "BookingConfirmed",
                Message = message,
                Channel = "Email",
                MessageId = messageId ?? Guid.NewGuid(),
                IsSent = false,
                SentAt = DateTime.UtcNow
            };

            await _notificationRepository.CreateAsync(notification, cancellationToken);

            _logger.LogInformation("Saved pending notification {NotificationId} for BookingId {BookingId}, UserId {UserId}, EventId {EventId}.",
                notification.Id, bookingId, userId, eventId);

            return Result<NotificationResponseDto>.Success(MapToDto(notification));
        }

        public async Task<Result<string>> ConfirmAndSendBookingNotificationAsync(
            Guid bookingId,
            Guid? messageId,
            CancellationToken cancellationToken = default)
        {
            if (bookingId == Guid.Empty)
            {
                return Result<string>.Failure(Error.Validation("BookingId cannot be empty."));
            }
            var notification = await _notificationRepository.MarkAsSentAsync(bookingId, messageId, cancellationToken);

            if (notification == null)
            {
                var existing = await _notificationRepository.GetByBookingIdAsync(bookingId, cancellationToken);
                if (existing == null)
                {
                    _logger.LogWarning("No pending notification found for BookingId {BookingId} upon payment success.", bookingId);
                    return Result<string>.Failure(Error.NotFound($"Notification for booking {bookingId} not found."));
                }

                _logger.LogInformation("Notification for BookingId {BookingId} has already been sent. Skipping duplicate.", bookingId);
                return Result<string>.Success("Notification was already sent.");
            }

            await _notificationSender.SendAsync(notification, cancellationToken);

            _logger.LogInformation("Successfully confirmed and sent notification {NotificationId} for BookingId {BookingId}.",
                notification.Id, bookingId);

            return Result<string>.Success("Booking confirmation notification sent successfully.");
        }

        public async Task<Result<string>> CancelPendingNotificationAsync(
            Guid bookingId,
            string? reason = null,
            Guid? messageId = null,
            CancellationToken cancellationToken = default)
        {
            if (bookingId == Guid.Empty)
            {
                return Result<string>.Failure(Error.Validation("BookingId cannot be empty."));
            }

            var notification = await _notificationRepository.MarkAsCancelledAsync(bookingId, reason ?? string.Empty, messageId, cancellationToken);

            if (notification == null)
            {
                var existing = await _notificationRepository.GetByBookingIdAsync(bookingId, cancellationToken);
                if (existing == null)
                {
                    _logger.LogWarning("No pending notification found for BookingId {BookingId} to cancel.", bookingId);
                    return Result<string>.Failure(Error.NotFound($"Notification for booking {bookingId} not found."));
                }

                _logger.LogInformation("Notification for BookingId {BookingId} has already been processed. Skipping duplicate.", bookingId);
                return Result<string>.Success("Notification was already processed.");
            }

            await _notificationSender.SendAsync(notification, cancellationToken);

            _logger.LogInformation("Successfully sent payment failure notification {NotificationId} for BookingId {BookingId}.",
                notification.Id, bookingId);

            return Result<string>.Success("Payment failure notification sent successfully.");
        }

        public async Task<Result<string>> ExpireBookingNotificationAsync(
            Guid bookingId,
            Guid? messageId = null,
            CancellationToken cancellationToken = default)
        {
            if (bookingId == Guid.Empty)
            {
                return Result<string>.Failure(Error.Validation("BookingId cannot be empty."));
            }

            var notification = await _notificationRepository.MarkAsExpiredAsync(bookingId, messageId, cancellationToken);

            if (notification == null)
            {
                var existing = await _notificationRepository.GetByBookingIdAsync(bookingId, cancellationToken);
                if (existing == null)
                {
                    _logger.LogWarning("No pending notification found for BookingId {BookingId} upon booking expiry.", bookingId);
                    return Result<string>.Failure(Error.NotFound($"Notification for booking {bookingId} not found."));
                }

                _logger.LogInformation("Notification for BookingId {BookingId} has already been processed. Skipping duplicate.", bookingId);
                return Result<string>.Success("Notification was already processed.");
            }

            await _notificationSender.SendAsync(notification, cancellationToken);

            _logger.LogInformation("Successfully sent booking expired notification {NotificationId} for BookingId {BookingId}.",
                notification.Id, bookingId);

            return Result<string>.Success("Booking expired notification sent successfully.");
        }

        public async Task<Result<string>> HandleEventCancelledNotificationAsync(
            Guid eventId,
            Guid? messageId = null,
            CancellationToken cancellationToken = default)
        {
            if (eventId == Guid.Empty)
            {
                return Result<string>.Failure(Error.Validation("EventId cannot be empty."));
            }

            var notifications = await _notificationRepository.GetByEventIdAsync(eventId, cancellationToken);
            if (notifications == null || notifications.Count == 0)
            {
                _logger.LogInformation("No bookings found for cancelled EventId {EventId}.", eventId);
                return Result<string>.Success("No bookings found for cancelled event.");
            }

            var activeBookings = notifications
                .Where(n => n.Type != "PaymentFailed" && n.Type != "BookingExpired" && n.Type != "EventCancelled")
                .ToList();

            if (activeBookings.Count == 0)
            {
                _logger.LogInformation("No active bookings to notify for cancelled EventId {EventId}.", eventId);
                return Result<string>.Success("No active bookings for cancelled event.");
            }

            int sentCount = 0;
            foreach (var booking in activeBookings)
            {
                var eventName = !string.IsNullOrWhiteSpace(booking.EventName) ? booking.EventName : "the event";

                if (!booking.IsSent)
                {
                    // Case 1: Pending booking was never confirmed: atomically transition it to EventCancelled
                    var updated = await _notificationRepository.MarkAsEventCancelledAsync(booking.Id, eventName, cancellationToken);
                    if (updated != null)
                    {
                        await _notificationSender.SendAsync(updated, cancellationToken);
                        sentCount++;
                    }
                }
                else if (booking.Type == "BookingConfirmed")
                {
                    // Case 2: Confirmed booking: check if cancellation notification was already created for this booking (idempotency)
                    var alreadyNotified = await _notificationRepository.ExistsByBookingIdAndTypeAsync(booking.BookingId, "EventCancelled", cancellationToken);
                    if (!alreadyNotified)
                    {
                        var cancellationNotification = new Notification
                        {
                            Id = Guid.NewGuid(),
                            BookingId = booking.BookingId,
                            UserId = booking.UserId,
                            EventId = eventId,
                            EventName = booking.EventName,
                            Type = "EventCancelled",
                            Message = $"The event '{eventName}' has been cancelled and your booking has been voided.",
                            Channel = "Email",
                            MessageId = messageId ?? Guid.NewGuid(),
                            IsSent = true,
                            SentAt = DateTime.UtcNow
                        };

                        await _notificationRepository.CreateAsync(cancellationNotification, cancellationToken);
                        await _notificationSender.SendAsync(cancellationNotification, cancellationToken);
                        sentCount++;
                    }
                }
            }

            _logger.LogInformation("Notified {Count} user(s) for cancelled EventId {EventId}.", sentCount, eventId);
            return Result<string>.Success($"Successfully processed cancellation notifications for {sentCount} booking(s).");
        }

        public async Task<Result<PagedResponse<NotificationResponseDto>>> GetUserNotificationsAsync(
            Guid userId,
            int pageNumber = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            if (pageNumber <= 0)
            {
                return Result<PagedResponse<NotificationResponseDto>>.Failure(Error.Validation("Page number must be greater than 0."));
            }

            if (pageSize <= 0 || pageSize > 100)
            {
                return Result<PagedResponse<NotificationResponseDto>>.Failure(Error.Validation("Page size must be between 1 and 100."));
            }

            if (userId == Guid.Empty)
            {
                return Result<PagedResponse<NotificationResponseDto>>.Failure(Error.Validation("A valid user ID is required."));
            }

            var paged = await _notificationRepository.GetByUserIdAsync(userId, pageNumber, pageSize, cancellationToken);
            var dtos = paged.Items.Select(MapToDto).ToList();

            var response = new PagedResponse<NotificationResponseDto>
            {
                Items = dtos,
                TotalCount = paged.TotalCount,
                PageNumber = paged.PageNumber,
                PageSize = paged.PageSize
            };

            return Result<PagedResponse<NotificationResponseDto>>.Success(response);
        }

        public async Task<Result<NotificationResponseDto>> GetNotificationByIdAsync(
            Guid id,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            if (id == Guid.Empty)
            {
                return Result<NotificationResponseDto>.Failure(Error.Validation("A valid notification ID is required."));
            }

            if (userId == Guid.Empty)
            {
                return Result<NotificationResponseDto>.Failure(Error.Validation("A valid user ID is required."));
            }

            var notification = await _notificationRepository.GetByIdAsync(id, cancellationToken);
            if (notification == null || notification.UserId != userId || !notification.IsSent)
            {
                return Result<NotificationResponseDto>.Failure(Error.NotFound("Notification not found."));
            }

            return Result<NotificationResponseDto>.Success(MapToDto(notification));
        }

        private static NotificationResponseDto MapToDto(Notification n) =>
            new(n.Id, n.BookingId, n.UserId, n.Type, n.Message, n.Channel, n.SentAt);
    }
}
