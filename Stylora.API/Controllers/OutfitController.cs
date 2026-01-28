using Microsoft.AspNetCore.Mvc;
using Stylora.Application.DTOs;
using Stylora.Application.Services;

namespace Stylora.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OutfitController : ControllerBase
{
    private readonly OutfitService _outfitService;

    public OutfitController(OutfitService outfitService)
    {
        _outfitService = outfitService;
    }

    [HttpPost("suggest")]
    public async Task<ActionResult<OutfitSuggestionResponse>> SuggestOutfit([FromBody] OutfitSuggestionRequest request)
    {
        try
        {
            var result = await _outfitService.SuggestOutfitAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
