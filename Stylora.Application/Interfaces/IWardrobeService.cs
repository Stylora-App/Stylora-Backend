using Stylora.Application.DTOs;

namespace Stylora.Application.Interfaces;

public interface IWardrobeService
{
    Task<IEnumerable<WardrobeItemDto>> GetAllItemsAsync(string userId);
    Task<WardrobeItemDto> AddItemAsync(string userId, CreateWardrobeItemRequest request);
    Task<bool> DeleteItemAsync(string userId, string itemId);
    Task IncrementWornCountAsync(string userId, string itemId);
}
