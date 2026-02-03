using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Domain.Entities;

namespace Stylora.Application.Services;

public class AnalysisService
{
    private readonly IGeminiService _geminiService;
    private readonly IWardrobeRepository _wardrobeRepository;
    private readonly ISeasonAnalysisRepository _analysisRepository;
    private readonly IUserRepository _userRepository;

    public AnalysisService(
        IGeminiService geminiService, 
        IWardrobeRepository wardrobeRepository,
        ISeasonAnalysisRepository analysisRepository,
        IUserRepository userRepository)
    {
        _geminiService = geminiService;
        _wardrobeRepository = wardrobeRepository;
        _analysisRepository = analysisRepository;
        _userRepository = userRepository;
    }

    public async Task<SeasonAnalysisResponse> AnalyzeSeasonAsync(string userId, SeasonAnalysisRequest request)
    {
        var result = await _geminiService.AnalyzeSeasonAsync(request.ImageBase64);
        
        // Get or create user
        var userGuid = await GetOrCreateUserGuidAsync(userId);
        
        // Persist to database
        var analysisResult = new SeasonAnalysisResult
        {
            UserId = userGuid,
            Season = result.Season,
            SubSeason = result.SubSeason,
            Description = result.Description,
            BestMetals = result.BestMetals,
            ConfidenceScore = result.ConfidenceScore,
            ImageData = request.ImageBase64 // Store the image for reference
        };
        
        await _analysisRepository.CreateAsync(analysisResult);
        
        return new SeasonAnalysisResponse
        {
            Id = analysisResult.Id.ToString(),
            Season = result.Season,
            SubSeason = result.SubSeason,
            Description = result.Description,
            RecommendedColors = result.RecommendedColors?.Select(rc => rc.Color?.HexCode ?? rc.Color?.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).ToList() ?? [],
            BestMetals = result.BestMetals
        };
    }

    public async Task<IEnumerable<SeasonAnalysisResponse>> GetAnalysisHistoryAsync(string userId)
    {
        var userGuid = await GetOrCreateUserGuidAsync(userId);
        var results = await _analysisRepository.GetByUserIdAsync(userGuid);
        
        return results.Select(r => new SeasonAnalysisResponse
        {
            Id = r.Id.ToString(),
            Season = r.Season,
            SubSeason = r.SubSeason,
            Description = r.Description,
            RecommendedColors = r.RecommendedColors?.Select(rc => rc.Color?.HexCode ?? rc.Color?.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).ToList() ?? [],
            BestMetals = r.BestMetals
        });
    }

    public async Task<SeasonAnalysisResponse?> GetLatestAnalysisAsync(string userId)
    {
        var userGuid = await GetOrCreateUserGuidAsync(userId);
        var result = await _analysisRepository.GetLatestByUserIdAsync(userGuid);
        
        if (result == null) return null;
        
        return new SeasonAnalysisResponse
        {
            Id = result.Id.ToString(),
            Season = result.Season,
            SubSeason = result.SubSeason,
            Description = result.Description,
            RecommendedColors = result.RecommendedColors?.Select(rc => rc.Color?.HexCode ?? rc.Color?.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).ToList() ?? [],
            BestMetals = result.BestMetals
        };
    }

    public async Task<UserProfileDto> SaveSeasonProfileAsync(string userId, SeasonAnalysisResponse analysis)
    {
        var profile = new UserProfile
        {
            Season = analysis.Season,
            SubSeason = analysis.SubSeason,
            PaletteColors = analysis.RecommendedColors?.Select(hexCode => new UserPaletteColor
            {
                Color = new Color { Name = hexCode, HexCode = hexCode }
            }).ToList() ?? []
        };

        var updatedProfile = await _wardrobeRepository.UpdateUserProfileAsync(userId, profile);
        
        return new UserProfileDto
        {
            Season = updatedProfile.Season,
            SubSeason = updatedProfile.SubSeason,
            Palette = updatedProfile.PaletteColors?.Select(pc => pc.Color?.HexCode ?? pc.Color?.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).ToList() ?? [],
            DisplayName = updatedProfile.DisplayName,
            PreferredStyle = updatedProfile.PreferredStyle
        };
    }

    private async Task<Guid> GetOrCreateUserGuidAsync(string userId)
    {
        // Try to parse as GUID first
        if (Guid.TryParse(userId, out var userGuid))
        {
            var existingUser = await _userRepository.GetByIdAsync(userGuid);
            if (existingUser != null)
                return userGuid;
        }
        
        // Try to find by email (userId might be email)
        var userByEmail = await _userRepository.GetByEmailAsync(userId);
        if (userByEmail != null)
            return userByEmail.Id;
        
        // Create a default anonymous user for the session
        var anonymousEmail = $"anonymous-{userId}@stylora.local";
        var existingAnonymous = await _userRepository.GetByEmailAsync(anonymousEmail);
        if (existingAnonymous != null)
            return existingAnonymous.Id;
        
        var newUser = new User
        {
            Email = anonymousEmail,
            PasswordHash = "anonymous",
            DisplayName = "Anonymous User"
        };
        var created = await _userRepository.CreateAsync(newUser);
        return created.Id;
    }
}
