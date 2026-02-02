using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stylora.Application.DTOs;
using Stylora.Application.Services;

namespace Stylora.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OutfitController : BaseApiController
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
            var result = await _outfitService.SuggestOutfitAsync(GetUserId(), request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("saved")]
    public async Task<ActionResult<IEnumerable<OutfitSuggestionResponse>>> GetSavedOutfits()
    {
        try
        {
            var outfits = await _outfitService.GetSavedOutfitsAsync(GetUserId());
            return Ok(outfits);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("save")]
    public async Task<ActionResult<OutfitSuggestionResponse>> SaveOutfit([FromBody] OutfitSuggestionResponse outfit)
    {
        try
        {
            var saved = await _outfitService.SaveOutfitAsync(GetUserId(), outfit);
            return Ok(saved);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteOutfit(string id)
    {
        try
        {
            var result = await _outfitService.DeleteOutfitAsync(GetUserId(), id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
