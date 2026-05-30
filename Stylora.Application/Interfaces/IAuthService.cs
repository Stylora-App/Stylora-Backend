using Stylora.Domain.Entities;

namespace Stylora.Application.Interfaces;

public interface IAuthService
{
    Task<User?> ValidateUserAsync(string email, string password);
    Task<User> RegisterUserAsync(string email, string password, string? firstName = null, string? lastName = null);
    Task<User?> GetUserByIdAsync(Guid userId);
    Task<User?> GetUserByEmailAsync(string email);
    Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
    Task UpdateLastLoginAsync(Guid userId);
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
    Task<User> FindOrCreateGoogleUserAsync(string googleId, string email, string? firstName, string? lastName);
}
