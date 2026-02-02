using Stylora.Domain.Entities;

namespace Stylora.Application.Interfaces;

public interface ISeasonAnalysisRepository
{
    Task<SeasonAnalysisResult?> GetByIdAsync(Guid id);
    Task<IEnumerable<SeasonAnalysisResult>> GetByUserIdAsync(Guid userId);
    Task<SeasonAnalysisResult?> GetLatestByUserIdAsync(Guid userId);
    Task<SeasonAnalysisResult> CreateAsync(SeasonAnalysisResult analysis);
    Task<SeasonAnalysisResult> UpdateAsync(SeasonAnalysisResult analysis);
    Task<bool> DeleteAsync(Guid id);
}
