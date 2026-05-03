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

    public async Task<Color?> ResolveColorAsync(string? colorName)
    {
        if (string.IsNullOrWhiteSpace(colorName))
        {
            return null;
        }

        var normalizedName = colorName.Trim().ToLowerInvariant();
        var existing = await _context.Colors.FirstOrDefaultAsync(color => color.Name == normalizedName);
        if (existing is not null)
        {
            return existing;
        }

        var color = new Color
        {
            Id = Guid.NewGuid(),
            Name = normalizedName
        };

        _context.Colors.Add(color);
        await _context.SaveChangesAsync();
        return color;
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

    public async Task<int> DeleteItemsAsync(string userId, IEnumerable<string> itemIds)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return 0;

        var itemGuids = itemIds
            .Select(id => Guid.TryParse(id, out var itemGuid) ? itemGuid : Guid.Empty)
            .Where(itemGuid => itemGuid != Guid.Empty)
            .Distinct()
            .ToList();

        if (itemGuids.Count == 0)
            return 0;

        var items = await _context.WardrobeItems
            .Where(w => w.UserId == userGuid && itemGuids.Contains(w.Id))
            .ToListAsync();

        if (items.Count == 0)
            return 0;

        _context.WardrobeItems.RemoveRange(items);
        await _context.SaveChangesAsync();
        return items.Count;
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
        existingItem.ArticleTypeLabel = item.ArticleTypeLabel;
        existingItem.AudienceTag = item.AudienceTag;
        existingItem.UsageTag = item.UsageTag;
        existingItem.ColorFamily = item.ColorFamily;
        existingItem.Style = item.Style;
        existingItem.ColorId = item.ColorId;
        existingItem.ValidationStatus = item.ValidationStatus;
        existingItem.ValidationConfidence = item.ValidationConfidence;
        existingItem.ValidationMessage = item.ValidationMessage;
        existingItem.ValidatedAt = item.ValidatedAt;
        existingItem.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return existingItem;
    }

}
