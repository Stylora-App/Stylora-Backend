using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Domain.Entities;

namespace Stylora.Application.Services;

public class WardrobeService
{
    private readonly IWardrobeRepository _wardrobeRepository;
    private readonly IGeminiService _geminiService;

    public WardrobeService(IWardrobeRepository wardrobeRepository, IGeminiService geminiService)
    {
        _wardrobeRepository = wardrobeRepository;
        _geminiService = geminiService;
    }

    public async Task<IEnumerable<WardrobeItemDto>> GetAllItemsAsync(string userId)
    {
        var items = await _wardrobeRepository.GetAllItemsAsync(userId);
        return items.Select(MapToDto);
    }

    public async Task<WardrobeItemDto> AddItemAsync(string userId, CreateWardrobeItemRequest request)
    {
        if (!Enum.TryParse<ClothingCategory>(request.Category, true, out var category))
        {
            category = ClothingCategory.Top;
        }

        var item = new WardrobeItem
        {
            Image = request.Image,
            Category = category,
            Tags = request.Tags,
            Description = null
        };

        var savedItem = await _wardrobeRepository.AddItemAsync(userId, item);
        return MapToDto(savedItem);
    }

    public async Task<bool> DeleteItemAsync(string userId, string itemId)
    {
        return await _wardrobeRepository.DeleteItemAsync(userId, itemId);
    }

    public async Task LogWearAsync(string userId, string itemId)
    {
        await _wardrobeRepository.LogWearAsync(userId, itemId);
    }

    public async Task<UserProfileDto> GetUserProfileAsync(string userId)
    {
        var profile = await _wardrobeRepository.GetUserProfileAsync(userId);
        return new UserProfileDto
        {
            Season = profile.Season,
            SubSeason = profile.SubSeason,
            Palette = profile.Palette,
            Name = profile.Name
        };
    }

    public async Task<UserProfileDto> UpdateUserProfileAsync(string userId, UserProfileDto profileDto)
    {
        var profile = new UserProfile
        {
            Season = profileDto.Season,
            SubSeason = profileDto.SubSeason,
            Palette = profileDto.Palette,
            Name = profileDto.Name
        };

        var updated = await _wardrobeRepository.UpdateUserProfileAsync(userId, profile);
        return new UserProfileDto
        {
            Season = updated.Season,
            SubSeason = updated.SubSeason,
            Palette = updated.Palette,
            Name = updated.Name
        };
    }

    private static WardrobeItemDto MapToDto(WardrobeItem item)
    {
        return new WardrobeItemDto
        {
            Id = item.Id,
            Image = item.Image,
            Category = item.Category.ToString().ToLower(),
            Tags = item.Tags,
            Color = item.Color,
            WearCount = item.WearCount,
            LastWorn = item.LastWorn?.ToString("o"),
            Description = item.Description
        };
    }
}
