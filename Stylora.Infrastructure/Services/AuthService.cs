using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Stylora.Application.Interfaces;
using Stylora.Application.Security;
using Stylora.Domain.Entities;

namespace Stylora.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;

    public AuthService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<User?> ValidateUserAsync(string email, string password)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null || !user.IsActive || user.PasswordHash == null)
            return null;

        if (!VerifyPassword(password, user.PasswordHash))
            return null;

        return user;
    }

    public async Task<User> RegisterUserAsync(string email, string password, string? firstName = null, string? lastName = null)
    {
        if (await _userRepository.EmailExistsAsync(email))
            throw new InvalidOperationException("Email is already registered.");

        if (!PasswordPolicy.IsValid(password))
            throw new InvalidOperationException(PasswordPolicy.ValidationMessage);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email.ToLowerInvariant(),
            PasswordHash = HashPassword(password),
            FirstName = firstName,
            LastName = lastName,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        return await _userRepository.CreateAsync(user);
    }

    public async Task<User> FindOrCreateGoogleUserAsync(string googleId, string email, string? firstName, string? lastName)
    {
        var user = await _userRepository.GetByGoogleIdAsync(googleId);
        if (user != null)
            return user;

        user = await _userRepository.GetByEmailAsync(email.ToLowerInvariant());
        if (user != null)
        {
            user.GoogleId = googleId;
            await _userRepository.UpdateAsync(user);
            return user;
        }

        user = new User
        {
            Id = Guid.NewGuid(),
            Email = email.ToLowerInvariant(),
            GoogleId = googleId,
            FirstName = firstName,
            LastName = lastName,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        return await _userRepository.CreateAsync(user);
    }

    public async Task<User?> GetUserByIdAsync(Guid userId)
    {
        return await _userRepository.GetByIdAsync(userId);
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _userRepository.GetByEmailAsync(email.ToLowerInvariant());
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        if (!PasswordPolicy.IsValid(newPassword))
            throw new InvalidOperationException(PasswordPolicy.ValidationMessage);

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null || user.PasswordHash == null)
            return false;

        if (!VerifyPassword(currentPassword, user.PasswordHash))
            return false;

        user.PasswordHash = HashPassword(newPassword);
        await _userRepository.UpdateAsync(user);
        return true;
    }

    public async Task UpdateLastLoginAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user != null)
        {
            user.LastLoginAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);
        }
    }

    public string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(128 / 8);
        string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: 100000,
            numBytesRequested: 256 / 8));
        return $"{Convert.ToBase64String(salt)}.{hashed}";
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        var parts = passwordHash.Split('.');
        if (parts.Length != 2)
            return false;

        var salt = Convert.FromBase64String(parts[0]);
        var hash = parts[1];

        string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: 100000,
            numBytesRequested: 256 / 8));

        return hash == hashed;
    }
}
