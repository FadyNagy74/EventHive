using AuthenticationService.Domain.Entities;
using EventHive.Shared.Extensions;
using AuthenticationService.Services;
using AuthenticationService.Services.DTOs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AuthenticationService.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthenticationController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAuthService _authService;

    public AuthenticationController(
        SignInManager<ApplicationUser> signInManager,
        IAuthService authService)
    {
        _signInManager = signInManager;
        _authService   = authService;
    }

    [AllowAnonymous]
    [HttpGet("google-login")]
    public IActionResult GoogleLogin()
    {
        var redirectUrl = Url.Action(nameof(GoogleResponse), "Authentication");
        var properties  = _signInManager.ConfigureExternalAuthenticationProperties(
            GoogleDefaults.AuthenticationScheme, redirectUrl);

        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }
    [AllowAnonymous]
    [HttpGet("google-response")]
    public async Task<IActionResult> GoogleResponse()
    {
        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info is null)
            return BadRequest("Could not retrieve external login information from Google.");

        // Clear the short-lived external cookie before issuing our own tokens
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        var result = await _authService.HandleGoogleCallbackAsync(info);

        if (!result.IsSuccess)
            return result.ToErrorResponse(this);

        return Ok(result.Value);
    }
    [AllowAnonymous]
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken(RefreshTokenRequest request)
    {
        var result = await _authService.RefreshTokenAsync(request.RefreshToken);

        if (!result.IsSuccess)
            return result.ToErrorResponse(this);

        return Ok(result.Value);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetProfile() {
        var userId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _authService.GetProfileAsync(userId);
        if (!result.IsSuccess)
            return result.ToErrorResponse(this);

        return Ok(result.Value);
    }

    [Authorize]
    [HttpPost("sign-out")]
    public async Task<IActionResult> Logout() {
        var userId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _authService.RevokeRefreshTokenAsync(userId);
        if (!result.IsSuccess)
            return result.ToErrorResponse(this);

        return Ok(result.Value);
    }
}

