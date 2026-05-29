using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Application.Security;

namespace Stylora.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;

    public AuthController(IAuthService authService, IUserService userService)
    {
        _authService = authService;
        _userService = userService;
    }

    /// <summary>Register a new user and start a session.</summary>
    /// <remarks>Creates the user account and sets the session cookie. No authentication required.</remarks>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = "Email and password are required."
                });
            }

            if (!PasswordPolicy.IsValid(request.Password))
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = PasswordPolicy.ValidationMessage
                });
            }

            var user = await _authService.RegisterUserAsync(
                request.Email, 
                request.Password, 
                request.FirstName, 
                request.LastName);

            await SignInUserAsync(user.Id, user.Email, false);

            return Ok(new AuthResponse
            {
                Success = true,
                Message = "Registration successful.",
                User = _userService.MapToUserDto(user)
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = ex.Message
            });
        }
    }

    /// <summary>Log in with email and password.</summary>
    /// <remarks>Validates credentials and sets the session cookie on success.</remarks>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = "Email and password are required."
            });
        }

        var user = await _authService.ValidateUserAsync(request.Email, request.Password);

        if (user == null)
        {
            return Unauthorized(new AuthResponse
            {
                Success = false,
                Message = "Invalid email or password."
            });
        }

        await _authService.UpdateLastLoginAsync(user.Id);
        await SignInUserAsync(user.Id, user.Email, request.RememberMe);

        return Ok(new AuthResponse
        {
            Success = true,
            Message = "Login successful.",
            User = _userService.MapToUserDto(user)
        });
    }

    /// <summary>Log out and clear the session cookie.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return Ok(new AuthResponse
        {
            Success = true,
            Message = "Logout successful."
        });
    }

    /// <summary>Get the currently authenticated user.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthResponse>> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new AuthResponse
            {
                Success = false,
                Message = "User not authenticated."
            });
        }

        var user = await _authService.GetUserByIdAsync(userId);

        if (user == null)
        {
            return NotFound(new AuthResponse
            {
                Success = false,
                Message = "User not found."
            });
        }

        return Ok(new AuthResponse
        {
            Success = true,
            User = _userService.MapToUserDto(user)
        });
    }

    /// <summary>Change the authenticated user's password.</summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new AuthResponse
            {
                Success = false,
                Message = "User not authenticated."
            });
        }

        if (!PasswordPolicy.IsValid(request.NewPassword))
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = PasswordPolicy.ValidationMessage
            });
        }

        try
        {
            var result = await _authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);

            if (!result)
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = "Current password is incorrect."
                });
            }

            return Ok(new AuthResponse
            {
                Success = true,
                Message = "Password changed successfully."
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = ex.Message
            });
        }
    }

    private async Task SignInUserAsync(Guid userId, string email, bool rememberMe)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, email)
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = rememberMe,
            ExpiresUtc = rememberMe 
                ? DateTimeOffset.UtcNow.AddDays(30) 
                : DateTimeOffset.UtcNow.AddHours(24),
            AllowRefresh = true
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);
    }
}

