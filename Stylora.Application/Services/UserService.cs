using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Application.Models;
using Stylora.Domain.Entities;
using Stylora.Domain.Enums;

namespace Stylora.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UserProfileDto> GetProfileAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return new UserProfileDto();

        var user = await _userRepository.GetByIdWithAnalysisAsync(userGuid);
        return user == null ? new UserProfileDto() : MapToProfileDto(user);
    }

    public async Task<UserProfileDto> UpdateProfileAsync(string userId, UpdateProfileRequest request)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return new UserProfileDto();

        StylePreference? style = Enum.TryParse<StylePreference>(request.Style, true, out var parsed) ? parsed : null;
        var user = await _userRepository.UpdateProfileAsync(userGuid, request.FirstName, request.LastName, request.ProfilePicture, style);
        return MapToProfileDto(user);
    }

    public UserDto MapToUserDto(User user) => BuildUserDto(user);

    public UserProfileDto MapToProfileDto(User user) => BuildProfileDto(user);

    public static UserDto BuildUserDto(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        ProfilePicture = user.ProfilePicture,
        Style = user.Style?.ToString().ToLowerInvariant(),
        CreatedAt = user.CreatedAt
    };

    public static UserProfileDto BuildProfileDto(User user)
    {
        var analysis = user.ColorAnalysisResult;
        var palette = SeasonData.GetPalette(analysis?.Season, analysis?.SubSeason);
        if (palette.Count == 0)
        {
            palette = analysis?.RecommendedColors
                ?.Select(rc => rc.Color?.HexCode ?? rc.Color?.Name ?? "")
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList() ?? [];
        }

        return new UserProfileDto
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            ProfilePicture = user.ProfilePicture,
            Style = user.Style?.ToString().ToLowerInvariant(),
            Season = analysis?.Season,
            SubSeason = analysis?.SubSeason,
            Palette = palette,
            BestMetals = analysis?.BestMetals,
            Undertone = DeriveUndertone(analysis?.Season),
            Contrast = DeriveContrast(analysis?.SubSeason)
        };
    }

    private static string? DeriveUndertone(string? season) => season?.ToLowerInvariant() switch
    {
        "spring" or "autumn" => "Warm",
        "summer" or "winter" => "Cool",
        _ => null
    };

    private static string? DeriveContrast(string? subSeason)
    {
        if (string.IsNullOrWhiteSpace(subSeason)) return null;
        var lower = subSeason.ToLowerInvariant();
        if (lower.Contains("soft") || lower.Contains("light")) return "Low–medium";
        if (lower.Contains("deep") || lower.Contains("dark") || lower.Contains("true") || lower.Contains("bright")) return "High";
        return "Medium";
    }
}
