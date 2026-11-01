using EventHive.Shared.Common;
using NotificationService.Services.DTOs.Responses;

namespace NotificationService.Services
{
    public interface INotificationService
    {
        Task<Result<NotificationResponseDto>> CreatePendingBookingNotificationAsync(
            Guid bookingId,
            Guid userId,
            Guid eventId,
            string eventName,
            DateTime eventDate,
            int quantity,
            Guid? messageId,
            CancellationToken cancellationToken = default);

        Task<Result<string>> ConfirmAndSendBookingNotificationAsync(
            Guid bookingId,
            Guid? messageId,
            CancellationToken cancellationToken = default);

        Task<Result<string>> CancelPendingNotificationAsync(
            Guid bookingId,
            string? reason = null,
            Guid? messageId = null,
            CancellationToken cancellationToken = default);

        Task<Result<string>> ExpireBookingNotificationAsync(
            Guid bookingId,
            Guid? messageId = null,
            CancellationToken cancellationToken = default);

        Task<Result<string>> HandleEventCancelledNotificationAsync(
            Guid eventId,
            Guid? messageId = null,
            CancellationToken cancellationToken = default);

        Task<Result<PagedResponse<NotificationResponseDto>>> GetUserNotificationsAsync(
            Guid userId,
            int pageNumber = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default);

        Task<Result<NotificationResponseDto>> GetNotificationByIdAsync(
            Guid id,
            Guid userId,
            CancellationToken cancellationToken = default);
    }
}
