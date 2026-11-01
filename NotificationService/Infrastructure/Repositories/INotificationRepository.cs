using EventHive.Shared.Common;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Repositories
{
    public interface INotificationRepository
    {
        Task CreateAsync(Notification notification, CancellationToken cancellationToken = default);
        Task UpdateAsync(Notification notification, CancellationToken cancellationToken = default);
        Task<bool> DeleteByBookingIdAsync(Guid bookingId, CancellationToken cancellationToken = default);
        Task<Notification?> GetByBookingIdAsync(Guid bookingId, CancellationToken cancellationToken = default);
        Task<Notification?> MarkAsSentAsync(Guid bookingId, Guid? messageId, CancellationToken cancellationToken = default);
        Task<Notification?> MarkAsCancelledAsync(Guid bookingId, string reason, Guid? messageId, CancellationToken cancellationToken = default);
        Task<Notification?> MarkAsExpiredAsync(Guid bookingId, Guid? messageId, CancellationToken cancellationToken = default);
        Task<Notification?> MarkAsEventCancelledAsync(Guid id, string eventName, CancellationToken cancellationToken = default);
        Task<List<Notification>> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default);
        Task<bool> ExistsByBookingIdAndTypeAsync(Guid bookingId, string type, CancellationToken cancellationToken = default);
        Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<PagedResponse<Notification>> GetByUserIdAsync(Guid userId, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    }
}
