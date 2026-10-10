using AuthenticationService.Domain.Entities;

namespace AuthenticationService.Infrastructure.Repositories;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token);
    Task<RefreshToken?> FindActiveByHashAsync(string tokenHash);
    Task RevokeAsync(RefreshToken token);
    Task RevokeAllForUserAsync(string userId);
}
