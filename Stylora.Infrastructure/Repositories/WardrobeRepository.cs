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

    public async Task<IEnumerable<WardrobeItem>> GetAllItemsAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return [];

        return await _context.WardrobeItems
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
        existingItem.Style = item.Style;
        existingItem.ColorId = item.ColorId;
        existingItem.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return existingItem;
    }

    public async Task IncrementWornCountAsync(string userId, string itemId)
    {
        if (!Guid.TryParse(userId, out var userGuid) || !Guid.TryParse(itemId, out var itemGuid))
            return;

        var item = await _context.WardrobeItems
            .FirstOrDefaultAsync(w => w.UserId == userGuid && w.Id == itemGuid);

        if (item != null)
        {
            item.WornCount++;
            item.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}
