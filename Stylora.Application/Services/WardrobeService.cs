using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Domain.Entities;
using Stylora.Domain.Enums;

namespace Stylora.Application.Services;

public class WardrobeService
{
    private readonly IWardrobeRepository _wardrobeRepository;
    private readonly IUserRepository _userRepository;

    public WardrobeService(IWardrobeRepository wardrobeRepository, IUserRepository userRepository)
    {
        _wardrobeRepository = wardrobeRepository;
        _userRepository = userRepository;
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

    public async Task<UserProfileDto> GetUserProfileAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return new UserProfileDto();

        var user = await _userRepository.GetByIdWithAnalysisAsync(userGuid);
        return user == null ? new UserProfileDto() : MapToProfileDto(user);
    }

    public async Task<UserProfileDto> UpdateUserProfileAsync(string userId, UpdateProfileRequest request)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return new UserProfileDto();

        StylePreference? style = Enum.TryParse<StylePreference>(request.Style, true, out var parsed) ? parsed : null;
        var user = await _userRepository.UpdateProfileAsync(userGuid, request.FirstName, request.LastName, request.ProfilePicture, style);
        return MapToProfileDto(user);
    }

    internal static UserProfileDto MapToProfileDto(User user)
    {
        var analysis = user.ColorAnalysisResult;
        return new UserProfileDto
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            ProfilePicture = user.ProfilePicture,
            Style = user.Style?.ToString().ToLowerInvariant(),
            SubSeason = analysis?.SubSeason,
            Palette = analysis?.RecommendedColors
                ?.Select(rc => rc.Color?.HexCode ?? rc.Color?.Name ?? "")
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList() ?? []
        };
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
