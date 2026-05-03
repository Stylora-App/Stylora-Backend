using Stylora.Application.DTOs;

namespace Stylora.Application.Interfaces;

public interface IOutfitChatService
{
    Task<OutfitChatResponse> ProcessAsync(string userId, OutfitChatRequest request);
}
