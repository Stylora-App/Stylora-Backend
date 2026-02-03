using Stylora.Domain.Entities;

namespace Stylora.Application.Interfaces;

public interface IWardrobeRepository
{
    Task<IEnumerable<WardrobeItem>> GetAllItemsAsync(string userId);
    Task<WardrobeItem?> GetItemByIdAsync(string userId, string itemId);
    Task<WardrobeItem> AddItemAsync(string userId, WardrobeItem item);
    Task<bool> DeleteItemAsync(string userId, string itemId);
    Task<WardrobeItem?> UpdateItemAsync(string userId, WardrobeItem item);
    Task<UserProfile> GetUserProfileAsync(string userId);
    Task<UserProfile> UpdateUserProfileAsync(string userId, UserProfile profile, List<string>? paletteHexCodes = null);
    Task LogWearAsync(string userId, string itemId);
}
