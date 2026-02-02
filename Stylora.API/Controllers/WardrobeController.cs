using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stylora.Application.DTOs;
using Stylora.Application.Services;

namespace Stylora.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WardrobeController : BaseApiController
{
    private readonly WardrobeService _wardrobeService;

    public WardrobeController(WardrobeService wardrobeService)
    {
        _wardrobeService = wardrobeService;
    }

    [HttpGet("items")]
    public async Task<ActionResult<IEnumerable<WardrobeItemDto>>> GetItems()
    {
        var items = await _wardrobeService.GetAllItemsAsync(GetUserId());
        return Ok(items);
    }

    [HttpPost("items")]
    public async Task<ActionResult<WardrobeItemDto>> AddItem([FromBody] CreateWardrobeItemRequest request)
    {
        var item = await _wardrobeService.AddItemAsync(GetUserId(), request);
        return CreatedAtAction(nameof(GetItems), new { id = item.Id }, item);
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

    [HttpPost("items/{id}/wear")]
    public async Task<IActionResult> LogWear(string id)
    {
        await _wardrobeService.LogWearAsync(GetUserId(), id);
        return Ok();
    }

    [HttpGet("profile")]
    public async Task<ActionResult<UserProfileDto>> GetProfile()
    {
        var profile = await _wardrobeService.GetUserProfileAsync(GetUserId());
        return Ok(profile);
    }

    [HttpPut("profile")]
    public async Task<ActionResult<UserProfileDto>> UpdateProfile([FromBody] UserProfileDto profile)
    {
        var updated = await _wardrobeService.UpdateUserProfileAsync(GetUserId(), profile);
        return Ok(updated);
    }
}
