using Stylora.Application.DTOs;
using Stylora.Domain.Entities;

namespace Stylora.Application.Interfaces;

public interface IUserService
{
    Task<UserProfileDto> GetProfileAsync(string userId);
    Task<UserProfileDto> UpdateProfileAsync(string userId, UpdateProfileRequest request);
    UserDto MapToUserDto(User user);
    UserProfileDto MapToProfileDto(User user);
}
