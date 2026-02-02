using Stylora.Domain.Entities;

namespace Stylora.Application.Interfaces;

public interface IOutfitRepository
{
    Task<OutfitSuggestion?> GetByIdAsync(Guid id);
    Task<IEnumerable<OutfitSuggestion>> GetByUserIdAsync(Guid userId);
    Task<IEnumerable<OutfitSuggestion>> GetFavoritesByUserIdAsync(Guid userId);
    Task<OutfitSuggestion> CreateAsync(OutfitSuggestion outfit);
    Task<OutfitSuggestion> UpdateAsync(OutfitSuggestion outfit);
    Task<bool> DeleteAsync(Guid id);
    Task<bool> ToggleFavoriteAsync(Guid id);
    Task<bool> RateAsync(Guid id, int rating);
}
