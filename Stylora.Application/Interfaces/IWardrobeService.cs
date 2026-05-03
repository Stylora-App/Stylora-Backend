using Stylora.Application.DTOs;

namespace Stylora.Application.Interfaces;

public interface IWardrobeService
{
    Task<IEnumerable<WardrobeItemDto>> GetAllItemsAsync(string userId);
    Task<WardrobeValidationDto> AnalyzeItemAsync(AnalyzeWardrobeItemRequest request);
    Task<CreateWardrobeItemResponse> AddItemAsync(string userId, CreateWardrobeItemRequest request);
    Task<bool> DeleteItemAsync(string userId, string itemId);
    Task<int> DeleteItemsAsync(string userId, IReadOnlyCollection<string> itemIds);
}
