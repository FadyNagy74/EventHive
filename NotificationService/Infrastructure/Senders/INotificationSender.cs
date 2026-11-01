using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Senders
{
    public interface INotificationSender
    {
        Task SendAsync(Notification notification, CancellationToken cancellationToken = default);
    }
}
