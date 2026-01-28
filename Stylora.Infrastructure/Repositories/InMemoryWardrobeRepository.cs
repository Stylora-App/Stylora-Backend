using System.Collections.Concurrent;
using Stylora.Application.Interfaces;
using Stylora.Domain.Entities;

namespace Stylora.Infrastructure.Repositories;

public class InMemoryWardrobeRepository : IWardrobeRepository
{
    // In-memory storage keyed by userId
    private readonly ConcurrentDictionary<string, List<WardrobeItem>> _items = new();
    private readonly ConcurrentDictionary<string, UserProfile> _profiles = new();

    public Task<IEnumerable<WardrobeItem>> GetAllItemsAsync(string userId)
    {
        var items = _items.GetOrAdd(userId, _ => []);
        return Task.FromResult<IEnumerable<WardrobeItem>>(items.ToList());
    }

    public Task<WardrobeItem?> GetItemByIdAsync(string userId, string itemId)
    {
        var items = _items.GetOrAdd(userId, _ => []);
        var item = items.FirstOrDefault(i => i.Id == itemId);
        return Task.FromResult(item);
    }

    public Task<WardrobeItem> AddItemAsync(string userId, WardrobeItem item)
    {
        var items = _items.GetOrAdd(userId, _ => []);
        
        if (string.IsNullOrEmpty(item.Id))
        {
            item.Id = Guid.NewGuid().ToString();
        }
        
        items.Add(item);
        return Task.FromResult(item);
    }

    public Task<bool> DeleteItemAsync(string userId, string itemId)
    {
        var items = _items.GetOrAdd(userId, _ => []);
        var item = items.FirstOrDefault(i => i.Id == itemId);
        
        if (item != null)
        {
            items.Remove(item);
            return Task.FromResult(true);
        }
        
        return Task.FromResult(false);
    }

    public Task<WardrobeItem?> UpdateItemAsync(string userId, WardrobeItem item)
    {
        var items = _items.GetOrAdd(userId, _ => []);
        var existingItem = items.FirstOrDefault(i => i.Id == item.Id);
        
        if (existingItem != null)
        {
            var index = items.IndexOf(existingItem);
            items[index] = item;
            return Task.FromResult<WardrobeItem?>(item);
        }
        
        return Task.FromResult<WardrobeItem?>(null);
    }

    public Task<UserProfile> GetUserProfileAsync(string userId)
    {
        var profile = _profiles.GetOrAdd(userId, _ => new UserProfile());
        return Task.FromResult(profile);
    }

    public Task<UserProfile> UpdateUserProfileAsync(string userId, UserProfile profile)
    {
        _profiles.AddOrUpdate(userId, profile, (_, _) => profile);
        return Task.FromResult(profile);
    }

    public Task LogWearAsync(string userId, string itemId)
    {
        var items = _items.GetOrAdd(userId, _ => []);
        var item = items.FirstOrDefault(i => i.Id == itemId);
        
        if (item != null)
        {
            item.WearCount++;
            item.LastWorn = DateTime.UtcNow;
        }
        
        return Task.CompletedTask;
    }
}
