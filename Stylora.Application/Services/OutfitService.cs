using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Domain.Entities;

namespace Stylora.Application.Services;

public class OutfitService
{
    private readonly IGeminiService _geminiService;

    public OutfitService(IGeminiService geminiService)
    {
        _geminiService = geminiService;
    }

    public async Task<OutfitSuggestionResponse> SuggestOutfitAsync(OutfitSuggestionRequest request)
    {
        var wardrobeItems = request.Items.Select(dto =>
        {
            if (!Enum.TryParse<ClothingCategory>(dto.Category, true, out var category))
            {
                category = ClothingCategory.Top;
            }

            return new WardrobeItem
            {
                Id = dto.Id,
                Category = category,
                Tags = dto.Tags,
                Color = dto.Color
            };
        });

        var suggestion = await _geminiService.SuggestOutfitAsync(
            wardrobeItems,
            request.Occasion,
            request.Weather
        );

        return new OutfitSuggestionResponse
        {
            TopId = suggestion.TopId,
            BottomId = suggestion.BottomId,
            ShoeId = suggestion.ShoeId,
            Reasoning = suggestion.Reasoning,
            StyleTip = suggestion.StyleTip
        };
    }
}
