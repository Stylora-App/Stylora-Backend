using Stylora.Application.DTOs;
using Stylora.Application.Models;

namespace Stylora.Application.Interfaces;

public interface IOutfitIntentParser
{
    Task<OutfitIntentResult> ParseAsync(
        IReadOnlyList<OutfitChatMessageDto> messages,
        CancellationToken cancellationToken = default);
}
