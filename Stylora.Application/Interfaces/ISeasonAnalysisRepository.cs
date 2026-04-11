using Stylora.Domain.Entities;

namespace Stylora.Application.Interfaces;

public interface ISeasonAnalysisRepository
{
    Task<SeasonAnalysisResult?> GetByIdAsync(Guid id);
    Task<SeasonAnalysisResult?> GetByUserIdAsync(Guid userId);
    Task<SeasonAnalysisResult> UpsertAsync(SeasonAnalysisResult analysis);
    Task<bool> DeleteAsync(Guid id);
}
