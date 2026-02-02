using Microsoft.EntityFrameworkCore;
using Stylora.Application.Interfaces;
using Stylora.Domain.Entities;
using Stylora.Infrastructure.Data;

namespace Stylora.Infrastructure.Repositories;

public class WardrobeRepository : IWardrobeRepository
{
    private readonly StyloraDbContext _context;

    public WardrobeRepository(StyloraDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<WardrobeItem>> GetAllItemsAsync(string oderId)
    {
        if (!Guid.TryParse(oderId, out var userGuid))
            return [];

        return await _context.WardrobeItems
            .Include(w => w.WardrobeItemTags)
                .ThenInclude(wt => wt.Tag)
            .Include(w => w.Color)
            .Where(w => w.UserId == userGuid)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();
    }

    public async Task<WardrobeItem?> GetItemByIdAsync(string userId, string itemId)
    {
        if (!Guid.TryParse(userId, out var userGuid) || !Guid.TryParse(itemId, out var itemGuid))
            return null;

        return await _context.WardrobeItems
            .Include(w => w.WardrobeItemTags)
                .ThenInclude(wt => wt.Tag)
            .Include(w => w.Color)
            .FirstOrDefaultAsync(w => w.UserId == userGuid && w.Id == itemGuid);
    }

    public async Task<WardrobeItem> AddItemAsync(string userId, WardrobeItem item)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            throw new ArgumentException("Invalid user ID", nameof(userId));

        item.Id = Guid.NewGuid();
        item.UserId = userGuid;
        item.CreatedAt = DateTime.UtcNow;

        _context.WardrobeItems.Add(item);
        await _context.SaveChangesAsync();
        return item;
    }

    public async Task<bool> DeleteItemAsync(string userId, string itemId)
    {
        if (!Guid.TryParse(userId, out var userGuid) || !Guid.TryParse(itemId, out var itemGuid))
            return false;

        var item = await _context.WardrobeItems
            .FirstOrDefaultAsync(w => w.UserId == userGuid && w.Id == itemGuid);

        if (item == null)
            return false;

        _context.WardrobeItems.Remove(item);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<WardrobeItem?> UpdateItemAsync(string userId, WardrobeItem item)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return null;

        var existingItem = await _context.WardrobeItems
            .FirstOrDefaultAsync(w => w.UserId == userGuid && w.Id == item.Id);

        if (existingItem == null)
            return null;

        existingItem.ImagePath = item.ImagePath;
        existingItem.Category = item.Category;
        existingItem.Description = item.Description;
        existingItem.Brand = item.Brand;
        existingItem.ColorId = item.ColorId;
        existingItem.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return existingItem;
    }

    public async Task<UserProfile> GetUserProfileAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return new UserProfile();

        var profile = await _context.UserProfiles
            .Include(p => p.PaletteColors)
                .ThenInclude(pc => pc.Color)
            .FirstOrDefaultAsync(p => p.UserId == userGuid);

        if (profile == null)
        {
            // First, ensure the user exists
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userGuid);
            if (user == null)
            {
                // Create an anonymous user
                user = new User
                {
                    Id = userGuid,
                    Email = $"anonymous-{userGuid}@stylora.local",
                    PasswordHash = "anonymous",
                    CreatedAt = DateTime.UtcNow
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }

            profile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = userGuid,
                CreatedAt = DateTime.UtcNow
            };
            _context.UserProfiles.Add(profile);
            await _context.SaveChangesAsync();
        }

        return profile;
    }

    public async Task<UserProfile> UpdateUserProfileAsync(string userId, UserProfile profile)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            throw new ArgumentException("Invalid user ID", nameof(userId));

        // Ensure user exists
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userGuid);
        if (user == null)
        {
            user = new User
            {
                Id = userGuid,
                Email = $"anonymous-{userGuid}@stylora.local",
                PasswordHash = "anonymous",
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        var existingProfile = await _context.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userGuid);

        if (existingProfile == null)
        {
            profile.Id = Guid.NewGuid();
            profile.UserId = userGuid;
            profile.CreatedAt = DateTime.UtcNow;
            _context.UserProfiles.Add(profile);
        }
        else
        {
            existingProfile.DisplayName = profile.DisplayName;
            existingProfile.Season = profile.Season;
            existingProfile.SubSeason = profile.SubSeason;
            existingProfile.AvatarPath = profile.AvatarPath;
            existingProfile.PreferredStyle = profile.PreferredStyle;
            existingProfile.UpdatedAt = DateTime.UtcNow;
            profile = existingProfile;
        }

        await _context.SaveChangesAsync();
        return profile;
    }

    public async Task LogWearAsync(string userId, string itemId)
    {
        if (!Guid.TryParse(userId, out var userGuid) || !Guid.TryParse(itemId, out var itemGuid))
            return;

        var item = await _context.WardrobeItems
            .FirstOrDefaultAsync(w => w.UserId == userGuid && w.Id == itemGuid);

        if (item != null)
        {
            var wearLog = new WearLog
            {
                Id = Guid.NewGuid(),
                WardrobeItemId = item.Id,
                WornAt = DateTime.UtcNow
            };
            _context.WearLogs.Add(wearLog);
            await _context.SaveChangesAsync();
        }
    }
}
