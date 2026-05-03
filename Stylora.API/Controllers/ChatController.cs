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

    [HttpPost("outfit")]
    public async Task<ActionResult<OutfitChatResponse>> GenerateOutfit([FromBody] OutfitChatRequest request)
    {
        var response = await _outfitChatService.ProcessAsync(GetUserId(), request);
        return Ok(response);
    }
}
