using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;

namespace Stylora.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WardrobeController : BaseApiController
{
    private readonly IWardrobeService _wardrobeService;

    public WardrobeController(IWardrobeService wardrobeService)
    {
        _wardrobeService = wardrobeService;
    }

    [HttpGet("items")]
    public async Task<ActionResult<IEnumerable<WardrobeItemDto>>> GetItems()
    {
        var items = await _wardrobeService.GetAllItemsAsync(GetUserId());
        return Ok(items);
    }

    [HttpPost("items/analyze")]
    public async Task<ActionResult<WardrobeValidationDto>> AnalyzeItem([FromBody] AnalyzeWardrobeItemRequest request)
    {
        try
        {
            var result = await _wardrobeService.AnalyzeItemAsync(request);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("items")]
    public async Task<ActionResult<CreateWardrobeItemResponse>> AddItem([FromBody] CreateWardrobeItemRequest request)
    {
        try
        {
            var result = await _wardrobeService.AddItemAsync(GetUserId(), request);
            if (result.Item is null && result.Validation is not null)
            {
                return Conflict(result.Validation);
            }

            return CreatedAtAction(nameof(GetItems), new { id = result.Item?.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("items/{id}")]
    public async Task<IActionResult> DeleteItem(string id)
    {
        var result = await _wardrobeService.DeleteItemAsync(GetUserId(), id);
        if (!result)
        {
            return NotFound();
        }
        return NoContent();
    }

    [HttpPost("items/delete-batch")]
    public async Task<IActionResult> DeleteItems([FromBody] DeleteWardrobeItemsRequest request)
    {
        if (request.ItemIds.Count == 0)
        {
            return BadRequest(new { message = "At least one wardrobe item id is required." });
        }

        var deletedCount = await _wardrobeService.DeleteItemsAsync(GetUserId(), request.ItemIds);
        return Ok(new { deletedCount });
    }
}
