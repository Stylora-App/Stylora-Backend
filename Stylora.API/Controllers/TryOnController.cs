using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stylora.Application.DTOs;
using Stylora.Application.Services;

namespace Stylora.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TryOnController : ControllerBase
{
    private readonly TryOnService _tryOnService;

    public TryOnController(TryOnService tryOnService)
    {
        _tryOnService = tryOnService;
    }

    [HttpPost("generate")]
    public async Task<ActionResult<TryOnResponse>> GenerateTryOn([FromBody] TryOnRequest request)
    {
        try
        {
            var result = await _tryOnService.GenerateTryOnAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
