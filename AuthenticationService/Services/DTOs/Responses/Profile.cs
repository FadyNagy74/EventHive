namespace AuthenticationService.Services.DTOs.Responses;

public sealed record Profile(
    string Id,
    string Email,
    string Name
);
