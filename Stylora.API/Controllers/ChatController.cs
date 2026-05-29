using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;

namespace Stylora.API.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController : BaseApiController
{
    private readonly IOutfitChatService _outfitChatService;

    public ChatController(IOutfitChatService outfitChatService)
    {
        _outfitChatService = outfitChatService;
    }

    /// <summary>Send conversation history and receive an outfit suggestion or follow-up question.</summary>
    /// <remarks>
    /// Passes the full message history to the Gemma intent worker which extracts occasion, style,
    /// weather, and gender. The backend then assembles an outfit from the user's wardrobe items.
    /// </remarks>
    [HttpPost("outfit")]
    [ProducesResponseType(typeof(OutfitChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<OutfitChatResponse>> GenerateOutfit([FromBody] OutfitChatRequest request)
    {
        var response = await _outfitChatService.ProcessAsync(GetUserId(), request);
        return Ok(response);
    }
}
