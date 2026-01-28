using Stylora.Domain.Entities;

namespace Stylora.Application.Interfaces;

public interface IGeminiService
{
    Task<SeasonAnalysisResult> AnalyzeSeasonAsync(string imageBase64);
    Task<string> DescribeClothingAsync(string imageBase64);
    Task<string> GenerateTryOnAsync(string personImageBase64, string clothingImageBase64);
    Task<OutfitSuggestion> SuggestOutfitAsync(IEnumerable<WardrobeItem> wardrobeItems, string occasion, string weather);
}
