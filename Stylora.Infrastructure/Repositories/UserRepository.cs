using Microsoft.EntityFrameworkCore;
using Stylora.Application.Interfaces;
using Stylora.Domain.Entities;
using Stylora.Domain.Enums;
using Stylora.Infrastructure.Data;

namespace Stylora.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly StyloraDbContext _context;

    public UserRepository(StyloraDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<User?> GetByIdWithAnalysisAsync(Guid id)
    {
        return await _context.Users
            .Include(u => u.ColorAnalysisResult)
                .ThenInclude(a => a!.RecommendedColors)
                    .ThenInclude(rc => rc.Color)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());
    }

    public async Task<User> CreateAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<User> UpdateAsync(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return false;

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.Users.AnyAsync(u => u.Email == email.ToLowerInvariant());
    }

    public async Task<User> UpdateProfileAsync(Guid userId, string? firstName, string? lastName, string? profilePicture, StylePreference? style)
    {
        var user = await _context.Users
            .Include(u => u.ColorAnalysisResult)
                .ThenInclude(a => a!.RecommendedColors)
                    .ThenInclude(rc => rc.Color)
            .FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new InvalidOperationException("User not found.");

        if (firstName != null) user.FirstName = firstName;
        if (lastName != null) user.LastName = lastName;
        if (profilePicture != null) user.ProfilePicture = profilePicture == string.Empty ? null : profilePicture;
        if (style.HasValue) user.Style = style;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return user;
    }
}
