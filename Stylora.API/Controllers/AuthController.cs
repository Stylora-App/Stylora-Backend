using System.Security.Claims;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Application.Security;
using Stylora.Infrastructure.Services;

namespace Stylora.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    IAuthService authService,
    IUserService userService,
    JwtService jwtService,
    GoogleClientIdSettings googleSettings) : ControllerBase
{
    /// <summary>Register a new user.</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new AuthResponse { Success = false, Message = "Email and password are required." });

        if (!PasswordPolicy.IsValid(request.Password))
            return BadRequest(new AuthResponse { Success = false, Message = PasswordPolicy.ValidationMessage });

        try
        {
            var user = await authService.RegisterUserAsync(request.Email, request.Password, request.FirstName, request.LastName);
            return Ok(new AuthResponse
            {
                Success = true,
                Message = "Registration successful.",
                AccessToken = jwtService.GenerateAccessToken(user),
                RefreshToken = jwtService.GenerateRefreshToken(user),
                User = userService.MapToUserDto(user)
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new AuthResponse { Success = false, Message = ex.Message });
        }
    }

    /// <summary>Log in with email and password.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new AuthResponse { Success = false, Message = "Email and password are required." });

        var user = await authService.ValidateUserAsync(request.Email, request.Password);
        if (user == null)
            return Unauthorized(new AuthResponse { Success = false, Message = "Invalid email or password." });

        await authService.UpdateLastLoginAsync(user.Id);
        return Ok(new AuthResponse
        {
            Success = true,
            Message = "Login successful.",
            AccessToken = jwtService.GenerateAccessToken(user),
            RefreshToken = jwtService.GenerateRefreshToken(user),
            User = userService.MapToUserDto(user)
        });
    }

    /// <summary>Sign in or register with a Google credential.</summary>
    [HttpPost("google")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Google([FromBody] GoogleAuthRequest request)
    {
        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(request.Credential,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [googleSettings.ClientId] });
        }
        catch
        {
            return Unauthorized(new AuthResponse { Success = false, Message = "Invalid Google credential." });
        }

        var user = await authService.FindOrCreateGoogleUserAsync(
            payload.Subject, payload.Email, payload.GivenName, payload.FamilyName);

        return Ok(new AuthResponse
        {
            Success = true,
            AccessToken = jwtService.GenerateAccessToken(user),
            RefreshToken = jwtService.GenerateRefreshToken(user),
            User = userService.MapToUserDto(user)
        });
    }

    /// <summary>Issue a new access token using a refresh token.</summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest request)
    {
        Guid userId;
        try
        {
            userId = jwtService.ValidateRefreshToken(request.RefreshToken);
        }
        catch (Exception ex)
        {
            return Unauthorized(new AuthResponse { Success = false, Message = $"Invalid refresh token: {ex.Message}" });
        }

        var user = await authService.GetUserByIdAsync(userId);
        if (user == null)
            return Unauthorized(new AuthResponse { Success = false, Message = "User not found." });

        return Ok(new AuthResponse
        {
            Success = true,
            AccessToken = jwtService.GenerateAccessToken(user),
            RefreshToken = jwtService.GenerateRefreshToken(user),
            User = userService.MapToUserDto(user)
        });
    }

    /// <summary>Log out — client should discard stored tokens.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public ActionResult<AuthResponse> Logout()
        => Ok(new AuthResponse { Success = true, Message = "Logout successful." });

    /// <summary>Get the currently authenticated user.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponse>> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new AuthResponse { Success = false, Message = "User not authenticated." });

        var user = await authService.GetUserByIdAsync(userId);
        if (user == null)
            return NotFound(new AuthResponse { Success = false, Message = "User not found." });

        return Ok(new AuthResponse { Success = true, User = userService.MapToUserDto(user) });
    }

    /// <summary>Change the authenticated user's password.</summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new AuthResponse { Success = false, Message = "User not authenticated." });

        if (!PasswordPolicy.IsValid(request.NewPassword))
            return BadRequest(new AuthResponse { Success = false, Message = PasswordPolicy.ValidationMessage });

        try
        {
            var result = await authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
            if (!result)
                return BadRequest(new AuthResponse { Success = false, Message = "Current password is incorrect." });

            return Ok(new AuthResponse { Success = true, Message = "Password changed successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new AuthResponse { Success = false, Message = ex.Message });
        }
    }
}
