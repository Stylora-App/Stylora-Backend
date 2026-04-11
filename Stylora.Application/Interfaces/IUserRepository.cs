using Stylora.Domain.Entities;
using Stylora.Domain.Enums;

namespace Stylora.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByIdWithAnalysisAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task<User> CreateAsync(User user);
    Task<User> UpdateAsync(User user);
    Task<bool> DeleteAsync(Guid id);
    Task<bool> EmailExistsAsync(string email);
    Task<User> UpdateProfileAsync(Guid userId, string? firstName, string? lastName, string? profilePicture, StylePreference? style);
}
