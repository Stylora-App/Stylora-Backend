using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Domain.Entities;
using Stylora.Domain.Enums;

namespace Stylora.Application.Services;

public class WardrobeService : IWardrobeService
{
    private readonly IWardrobeRepository _wardrobeRepository;

    public WardrobeService(IWardrobeRepository wardrobeRepository)
    {
        _wardrobeRepository = wardrobeRepository;
    }

    public async Task<IEnumerable<WardrobeItemDto>> GetAllItemsAsync(string userId)
    {
        var items = await _wardrobeRepository.GetAllItemsAsync(userId);
        return items.Select(MapToDto);
    }

    public async Task<WardrobeItemDto> AddItemAsync(string userId, CreateWardrobeItemRequest request)
    {
        if (!Enum.TryParse<ClothingCategory>(request.Category, true, out var category))
            category = ClothingCategory.Top;

        var item = new WardrobeItem
        {
            ImagePath = request.Image,
            Category = category,
            Style = Enum.TryParse<StylePreference>(request.Style, true, out var style) ? style : null
        };

        var savedItem = await _wardrobeRepository.AddItemAsync(userId, item);
        return MapToDto(savedItem);
    }

    public async Task<bool> DeleteItemAsync(string userId, string itemId)
    {
        return await _wardrobeRepository.DeleteItemAsync(userId, itemId);
    }

    public async Task IncrementWornCountAsync(string userId, string itemId)
    {
        await _wardrobeRepository.IncrementWornCountAsync(userId, itemId);
    }

    private static WardrobeItemDto MapToDto(WardrobeItem item)
    {
        return new WardrobeItemDto
        {
            Id = item.Id.ToString(),
            Image = item.ImagePath,
            Category = item.Category.ToString().ToLowerInvariant(),
            Style = item.Style?.ToString().ToLowerInvariant(),
            Color = item.Color?.HexCode ?? item.Color?.Name,
            WornCount = item.WornCount
        };
    }
}
