using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;

namespace Stylora.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TryOnController : BaseApiController
{
    private readonly ITryOnService _tryOnService;

    public TryOnController(ITryOnService tryOnService)
    {
        _tryOnService = tryOnService;
    }

    /// <summary>Generate a virtual try-on image.</summary>
    /// <remarks>
    /// Sends the person photo and clothing image (or URL) to Google Gemini to produce a
    /// composite try-on image. The person photo is saved for reuse via GET /last-photo.
    /// </remarks>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(TryOnResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TryOnResponse>> GenerateTryOn([FromBody] TryOnRequest request)
    {
        var userId = GetUserGuid();
        if (userId is null) return Unauthorized();

        try
        {
            var result = await _tryOnService.GenerateTryOnAsync(request, userId.Value);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Get the last person photo used by the authenticated user.</summary>
    [HttpGet("last-photo")]
    [ProducesResponseType(typeof(LastTryOnPhotoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LastTryOnPhotoDto>> GetLastPhoto()
    {
        var userId = GetUserGuid();
        if (userId is null) return Unauthorized();

        var dto = await _tryOnService.GetLastPersonPhotoAsync(userId.Value);
        if (dto is null) return NotFound();

        return Ok(dto);
    }
}
