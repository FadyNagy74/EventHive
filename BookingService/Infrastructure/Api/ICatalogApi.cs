using BookingService.Services.DTOs.Responses;
using EventHive.Shared.Common;

namespace BookingService.Infrastructure.Api
{
    public interface ICatalogApi
    {
        Task<Result<ReserveSeatsResponseDto>> ReserveSeatsAsync(Guid? eventId, string eventName, int? quantity, CancellationToken cancellationToken = default);
        Task<Result<string>> ReleaseSeatsAsync(Guid? eventId, int? quantity, CancellationToken cancellationToken = default);
    }
}
