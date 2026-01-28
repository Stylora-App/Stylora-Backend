using Microsoft.AspNetCore.Mvc;
using Stylora.Application.DTOs;
using Stylora.Application.Services;

namespace Stylora.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WardrobeController : ControllerBase
{
    private readonly WardrobeService _wardrobeService;

    public WardrobeController(WardrobeService wardrobeService)
    {
        _wardrobeService = wardrobeService;
    }

    // For simplicity, using a default userId. In production, this would come from authentication.
    private const string DefaultUserId = "default-user";

    [HttpGet("items")]
    public async Task<ActionResult<IEnumerable<WardrobeItemDto>>> GetItems()
    {
        var items = await _wardrobeService.GetAllItemsAsync(DefaultUserId);
        return Ok(items);
    }

    [HttpPost("items")]
    public async Task<ActionResult<WardrobeItemDto>> AddItem([FromBody] CreateWardrobeItemRequest request)
    {
        var item = await _wardrobeService.AddItemAsync(DefaultUserId, request);
        return CreatedAtAction(nameof(GetItems), new { id = item.Id }, item);
    }

    [HttpDelete("items/{id}")]
    public async Task<IActionResult> DeleteItem(string id)
    {
        var result = await _wardrobeService.DeleteItemAsync(DefaultUserId, id);
        if (!result)
        {
            return NotFound();
        }
        return NoContent();
    }

    [HttpPost("items/{id}/wear")]
    public async Task<IActionResult> LogWear(string id)
    {
        await _wardrobeService.LogWearAsync(DefaultUserId, id);
        return Ok();
    }

    [HttpGet("profile")]
    public async Task<ActionResult<UserProfileDto>> GetProfile()
    {
        var profile = await _wardrobeService.GetUserProfileAsync(DefaultUserId);
        return Ok(profile);
    }

    [HttpPut("profile")]
    public async Task<ActionResult<UserProfileDto>> UpdateProfile([FromBody] UserProfileDto profile)
    {
        var updated = await _wardrobeService.UpdateUserProfileAsync(DefaultUserId, profile);
        return Ok(updated);
    }
}
