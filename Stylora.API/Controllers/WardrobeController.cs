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

    /// <summary>List all wardrobe items for the authenticated user.</summary>
    [HttpGet("items")]
    [ProducesResponseType(typeof(IEnumerable<WardrobeItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<WardrobeItemDto>>> GetItems()
    {
        var items = await _wardrobeService.GetAllItemsAsync(GetUserId());
        return Ok(items);
    }

    /// <summary>Validate a clothing image without saving it.</summary>
    /// <remarks>Runs the CLIP embedding worker to classify the image and return confidence, suggested labels, and category.</remarks>
    [HttpPost("items/analyze")]
    [ProducesResponseType(typeof(WardrobeValidationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

    /// <summary>Add a new wardrobe item.</summary>
    /// <remarks>
    /// Runs CLIP validation before saving. Returns 409 when the image fails the clothing check —
    /// set <c>overrideValidationWarning=true</c> in the request body to force-add anyway.
    /// </remarks>
    [HttpPost("items")]
    [ProducesResponseType(typeof(CreateWardrobeItemResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(WardrobeValidationDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

    /// <summary>Delete a single wardrobe item.</summary>
    /// <param name="id">The wardrobe item ID to delete.</param>
    [HttpDelete("items/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteItem(string id)
    {
        var result = await _wardrobeService.DeleteItemAsync(GetUserId(), id);
        if (!result)
        {
            return NotFound();
        }
        return NoContent();
    }

    /// <summary>Delete multiple wardrobe items in one request.</summary>
    [HttpPost("items/delete-batch")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
