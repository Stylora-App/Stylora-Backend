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

    [HttpPost("generate")]
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

    [HttpGet("last-photo")]
    public async Task<ActionResult<LastTryOnPhotoDto>> GetLastPhoto()
    {
        var userId = GetUserGuid();
        if (userId is null) return Unauthorized();

        var dto = await _tryOnService.GetLastPersonPhotoAsync(userId.Value);
        if (dto is null) return NotFound();

        return Ok(dto);
    }
}
