using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Domain.Entities;

namespace Stylora.Application.Services;

public class AnalysisService
{
    private readonly IGeminiService _geminiService;
    private readonly ISeasonAnalysisRepository _analysisRepository;
    private readonly IUserRepository _userRepository;

    public AnalysisService(
        IGeminiService geminiService,
        ISeasonAnalysisRepository analysisRepository,
        IUserRepository userRepository)
    {
        _geminiService = geminiService;
        _analysisRepository = analysisRepository;
        _userRepository = userRepository;
    }

    public async Task<SeasonAnalysisResponse> AnalyzeSeasonAsync(string userId, SeasonAnalysisRequest request)
    {
        var result = await _geminiService.AnalyzeSeasonAsync(request.ImageBase64);

        return new SeasonAnalysisResponse
        {
            Season = result.Season,
            SubSeason = result.SubSeason,
            Description = result.Description,
            RecommendedColors = result.RecommendedColors
                ?.Select(rc => rc.Color?.HexCode ?? rc.Color?.Name ?? "")
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList() ?? [],
            BestMetals = result.BestMetals
        };
    }

    public async Task<SeasonAnalysisResponse?> GetLatestAnalysisAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return null;

        var result = await _analysisRepository.GetByUserIdAsync(userGuid);
        if (result == null) return null;

        return new SeasonAnalysisResponse
        {
            Id = result.Id.ToString(),
            Season = result.Season,
            SubSeason = result.SubSeason,
            Description = result.Description,
            RecommendedColors = result.RecommendedColors
                ?.Select(rc => rc.Color?.HexCode ?? rc.Color?.Name ?? "")
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList() ?? [],
            BestMetals = result.BestMetals
        };
    }

    public async Task<UserProfileDto> SaveSeasonProfileAsync(string userId, SeasonAnalysisResponse analysis)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            throw new ArgumentException("Invalid user ID", nameof(userId));

        var analysisResult = new SeasonAnalysisResult
        {
            UserId = userGuid,
            Season = analysis.Season,
            SubSeason = analysis.SubSeason,
            Description = analysis.Description,
            BestMetals = analysis.BestMetals,
            RecommendedColors = analysis.RecommendedColors
                .Select(hexCode => new RecommendedColor
                {
                    Color = new Color { Name = hexCode, HexCode = hexCode }
                }).ToList()
        };

        await _analysisRepository.UpsertAsync(analysisResult);

        var user = await _userRepository.GetByIdWithAnalysisAsync(userGuid)
            ?? throw new InvalidOperationException("User not found.");

        return WardrobeService.MapToProfileDto(user);
    }
}
