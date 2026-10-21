using AuthenticationService.Services.DTOs.Responses;
using EventHive.Shared.Common;
using Microsoft.AspNetCore.Identity;

namespace AuthenticationService.Services;

public interface IAuthService
{
    Task<Result<AuthResponse>> HandleGoogleCallbackAsync(ExternalLoginInfo info);
    Task<Result<Profile>> GetProfileAsync(string? userId);
    Task<Result<AuthResponse>> RefreshTokenAsync(string rawRefreshToken);
    Task<Result<string>> RevokeRefreshTokenAsync(string userId);
}
