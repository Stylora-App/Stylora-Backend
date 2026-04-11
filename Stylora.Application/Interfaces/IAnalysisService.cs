using Stylora.Application.DTOs;

namespace Stylora.Application.Interfaces;

public interface IAnalysisService
{
    Task<SeasonAnalysisResponse> AnalyzeSeasonAsync(string userId, SeasonAnalysisRequest request);
    Task<SeasonAnalysisResponse?> GetLatestAnalysisAsync(string userId);
    Task<UserProfileDto> SaveSeasonProfileAsync(string userId, SeasonAnalysisResponse analysis);
}
