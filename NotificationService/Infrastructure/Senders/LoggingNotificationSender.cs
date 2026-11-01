using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Senders
{
    public class LoggingNotificationSender : INotificationSender
    {
        private readonly ILogger<LoggingNotificationSender> _logger;

        public LoggingNotificationSender(ILogger<LoggingNotificationSender> logger)
        {
            _logger = logger;
        }

        public Task SendAsync(Notification notification, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "[{Channel} NOTIFICATION DISPATCHED] To UserId: {UserId} | Type: {Type} | BookingId: {BookingId} | Body: \"{Message}\"",
                notification.Channel.ToUpperInvariant(), notification.UserId, notification.Type, notification.BookingId, notification.Message);

            return Task.CompletedTask;
        }
    }
}
