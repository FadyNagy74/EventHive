namespace AuthenticationService.Services.DTOs;

public sealed record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    string UserId,
    string Email,
    string Name
);
