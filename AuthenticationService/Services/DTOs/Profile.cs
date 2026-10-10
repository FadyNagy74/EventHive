namespace AuthenticationService.Services.DTOs;

public sealed record Profile(
    string Id,
    string Email,
    string Name
);