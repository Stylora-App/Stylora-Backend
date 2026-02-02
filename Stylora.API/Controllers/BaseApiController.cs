using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Stylora.API.Controllers;

/// <summary>
/// Base controller with common authentication helpers
/// </summary>
public abstract class BaseApiController : ControllerBase
{
    /// <summary>
    /// Gets the authenticated user's ID from the cookie claims.
    /// Falls back to a default user ID for anonymous access during development.
    /// </summary>
    protected string GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        // Return authenticated user ID if available
        if (!string.IsNullOrEmpty(userIdClaim))
        {
            return userIdClaim;
        }
        
        // Fallback for development/anonymous access
        return "00000000-0000-0000-0000-000000000001";
    }

    /// <summary>
    /// Gets the authenticated user's ID as a Guid.
    /// Returns null if user is not authenticated.
    /// </summary>
    protected Guid? GetUserGuid()
    {
        var userId = GetUserId();
        return Guid.TryParse(userId, out var guid) ? guid : null;
    }

    /// <summary>
    /// Checks if the current request is from an authenticated user.
    /// </summary>
    protected bool IsAuthenticated => User.Identity?.IsAuthenticated ?? false;
}
