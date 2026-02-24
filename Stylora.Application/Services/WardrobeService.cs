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
            ImagePath = request.Image,
            Category = category,
            Description = request.Description,
            Brand = request.Brand
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
            Palette = profile.PaletteColors?.OrderBy(pc => pc.DisplayOrder).Select(pc => pc.Color?.HexCode ?? pc.Color?.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).ToList() ?? [],
            DisplayName = profile.DisplayName,
            PreferredStyle = profile.PreferredStyle,
            ProfilePicture = profile.AvatarPath
        };
    }

    public async Task<UserProfileDto> UpdateUserProfileAsync(string userId, UserProfileDto profileDto)
    {
        var profile = new UserProfile
        {
            Season = profileDto.Season,
            SubSeason = profileDto.SubSeason,
            DisplayName = profileDto.DisplayName,
            PreferredStyle = profileDto.PreferredStyle,
            AvatarPath = profileDto.ProfilePicture
        };

        var updated = await _wardrobeRepository.UpdateUserProfileAsync(userId, profile, profileDto.Palette);
        return new UserProfileDto
        {
            Season = updated.Season,
            SubSeason = updated.SubSeason,
            Palette = updated.PaletteColors?.OrderBy(pc => pc.DisplayOrder).Select(pc => pc.Color?.HexCode ?? pc.Color?.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).ToList() ?? [],
            DisplayName = updated.DisplayName,
            PreferredStyle = updated.PreferredStyle,
            ProfilePicture = updated.AvatarPath
        };
    }

    private static WardrobeItemDto MapToDto(WardrobeItem item)
    {
        return new WardrobeItemDto
        {
            Id = item.Id.ToString(),
            Image = item.ImagePath,
            Category = item.Category.ToString().ToLower(),
            Tags = item.WardrobeItemTags?.Select(wt => wt.Tag?.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).ToList() ?? [],
            Color = item.Color?.Name,
            Brand = item.Brand,
            WearCount = item.WearLogs?.Count ?? 0,
            LastWorn = item.WearLogs?.OrderByDescending(w => w.WornAt).FirstOrDefault()?.WornAt.ToString("o"),
            Description = item.Description
        };
    }
}
