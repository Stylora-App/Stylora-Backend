using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
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
        return new UserProfileDto
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            ProfilePicture = user.ProfilePicture,
            Style = user.Style?.ToString().ToLowerInvariant(),
            Season = analysis?.Season,
            SubSeason = analysis?.SubSeason,
            Palette = analysis?.RecommendedColors
                ?.Select(rc => rc.Color?.HexCode ?? rc.Color?.Name ?? "")
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList() ?? []
        };
    }
}
