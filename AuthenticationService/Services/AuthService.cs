using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AuthenticationService.Domain.Entities;
using AuthenticationService.Infrastructure.Repositories;
using AuthenticationService.Services.DTOs;
using EventHive.Shared.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace AuthenticationService.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _configuration;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IRefreshTokenRepository refreshTokenRepository,
        IConfiguration configuration,
        RoleManager<IdentityRole> roleManager
        )
    {
        _userManager              = userManager;
        _refreshTokenRepository   = refreshTokenRepository;
        _configuration            = configuration;
        _roleManager              = roleManager;

    }

    // ── Google OAuth2 callback ─────────────────────────────────────────────────

    public async Task<Result<AuthResponse>> HandleGoogleCallbackAsync(ExternalLoginInfo info)
    {
        var email     = info.Principal.FindFirstValue(ClaimTypes.Email);
        var givenName = info.Principal.FindFirstValue(ClaimTypes.GivenName);
        var fullName  = info.Principal.FindFirstValue(ClaimTypes.Name);

        var name = !string.IsNullOrWhiteSpace(givenName) ? givenName
                 : !string.IsNullOrWhiteSpace(fullName)  ? fullName
                 : email!.Split('@')[0];

        if (string.IsNullOrEmpty(email))
            return Result<AuthResponse>.Failure(
                Error.Validation("Google did not provide an email address for this account."));

        var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        if (user is null)
        {
            user = await _userManager.FindByEmailAsync(email);

            if (user is null)
            {

                var namePart  = System.Text.RegularExpressions.Regex
                    .Replace(name.ToLowerInvariant(), @"[^a-z0-9]", ".");  // "john.doe"
                var emailPart = email.Split('@')[0]
                    .ToLowerInvariant();                                     // "john123"
                var userName  = $"{namePart}_{emailPart}";

                user = new ApplicationUser
                {
                    UserName  = userName,
                    Email     = email,
                    Name      = fullName ?? name,
                    CreatedAt = DateTime.UtcNow
                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                    return Result<AuthResponse>.Failure(
                        createResult.Errors.Select(e => Error.Failure(e.Description)).ToList());
                await _userManager.AddToRoleAsync(user, "User");
            }

            var addLoginResult = await _userManager.AddLoginAsync(user, info);
            if (!addLoginResult.Succeeded)
                return Result<AuthResponse>.Failure(
                    addLoginResult.Errors.Select(e => Error.Failure(e.Description)).ToList());
        }

        return await IssueTokenPairAsync(user);
    }


    public async Task<Result<AuthResponse>> RefreshTokenAsync(string rawRefreshToken)
    {
        var tokenHash     = HashToken(rawRefreshToken);
        var existingToken = await _refreshTokenRepository.FindActiveByHashAsync(tokenHash);

        if (existingToken is null)
            return Result<AuthResponse>.Failure(
                Error.Unauthorized("Refresh token is invalid, expired, or has already been used."));

        var user = existingToken.User;
        await _refreshTokenRepository.RevokeAsync(existingToken);

        return await IssueTokenPairAsync(user);
    }

    public async Task<Result<Profile>> GetProfileAsync(string? userId) {
        if (string.IsNullOrEmpty(userId))
            return Result<Profile>.Failure(Error.Unauthorized("Invalid token."));

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return Result<Profile>.Failure(Error.NotFound("User not found."));
        return Result<Profile>.Success(new Profile(
            Id: user.Id,
            Email: user.Email!,
            Name: user.Name
        ));
    }

    public async Task<Result<string>> RevokeRefreshTokenAsync(string? userId)
    {
        if (string.IsNullOrEmpty(userId))
            return Result<string>.Failure(Error.Unauthorized("Invalid token."));

        await _refreshTokenRepository.RevokeAllForUserAsync(userId);
        return Result<string>.Success("Refresh token revoked successfully.");
    }

    private async Task<Result<AuthResponse>> IssueTokenPairAsync(ApplicationUser user)
    {
        var (accessToken, expiresAt) = await GenerateAccessToken(user);
        var rawRefreshToken = GenerateRefreshToken();
        var tokenHash       = HashToken(rawRefreshToken);
        var refreshDays     = int.Parse(_configuration["JWT:RefreshTokenExpiryDays"] ?? "14");

        await _refreshTokenRepository.AddAsync(new RefreshToken
        {
            UserId    = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshDays)
        });

        return Result<AuthResponse>.Success(new AuthResponse(
            AccessToken:          accessToken,
            AccessTokenExpiresAt: expiresAt,
            RefreshToken:         rawRefreshToken,
            UserId:               user.Id,
            Email:                user.Email!,
            Name:                 user.Name
        ));
    }


    // ── Private helpers ────────────────────────────────────────────────────────

    private async Task<(string Token, DateTime ExpiresAt)> GenerateAccessToken(ApplicationUser user)
    {
        var expiryMinutes = int.Parse(_configuration["JWT:ExpiryMinutes"] ?? "30");
        var expiresAt     = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(ClaimTypes.Name,               user.Name),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
        };

        var roles = await _userManager.GetRolesAsync(user);
    foreach (var role in roles)
    {
        claims.Add(new Claim(ClaimTypes.Role, role));
    }

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:SecretKey"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:             _configuration["JWT:Issuer"],
            audience:           _configuration["JWT:Audience"],
            claims:             claims,
            expires:            expiresAt,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }
}
