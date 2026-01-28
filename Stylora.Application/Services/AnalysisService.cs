using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Domain.Entities;

namespace Stylora.Application.Services;

public class AnalysisService
{
    private readonly IGeminiService _geminiService;
    private readonly IWardrobeRepository _wardrobeRepository;

    public AnalysisService(IGeminiService geminiService, IWardrobeRepository wardrobeRepository)
    {
        _geminiService = geminiService;
        _wardrobeRepository = wardrobeRepository;
    }

    public async Task<SeasonAnalysisResponse> AnalyzeSeasonAsync(SeasonAnalysisRequest request)
    {
        var result = await _geminiService.AnalyzeSeasonAsync(request.ImageBase64);
        
        return new SeasonAnalysisResponse
        {
            Season = result.Season,
            SubSeason = result.SubSeason,
            Description = result.Description,
            RecommendedColors = result.RecommendedColors,
            BestMetals = result.BestMetals
        };
    }

    public async Task<UserProfileDto> SaveSeasonProfileAsync(string userId, SeasonAnalysisResponse analysis)
    {
        var profile = new UserProfile
        {
            Season = analysis.Season,
            SubSeason = analysis.SubSeason,
            Palette = analysis.RecommendedColors
        };

        var updatedProfile = await _wardrobeRepository.UpdateUserProfileAsync(userId, profile);
        
        return new UserProfileDto
        {
            Season = updatedProfile.Season,
            SubSeason = updatedProfile.SubSeason,
            Palette = updatedProfile.Palette,
            Name = updatedProfile.Name
        };
    }
}
