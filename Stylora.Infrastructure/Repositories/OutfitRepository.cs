using Microsoft.EntityFrameworkCore;
using Stylora.Application.Interfaces;
using Stylora.Domain.Entities;
using Stylora.Infrastructure.Data;

namespace Stylora.Infrastructure.Repositories;

public class OutfitRepository : IOutfitRepository
{
    private readonly StyloraDbContext _context;

    public OutfitRepository(StyloraDbContext context)
    {
        _context = context;
    }

    public async Task<OutfitSuggestion?> GetByIdAsync(Guid id)
    {
        return await _context.OutfitSuggestions
            .Include(o => o.OutfitItems)
                .ThenInclude(oi => oi.WardrobeItem)
                    .ThenInclude(w => w.Color)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<IEnumerable<OutfitSuggestion>> GetByUserIdAsync(Guid userId)
    {
        return await _context.OutfitSuggestions
            .Include(o => o.OutfitItems)
                .ThenInclude(oi => oi.WardrobeItem)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<OutfitSuggestion>> GetFavoritesByUserIdAsync(Guid userId)
    {
        return await _context.OutfitSuggestions
            .Include(o => o.OutfitItems)
                .ThenInclude(oi => oi.WardrobeItem)
            .Where(o => o.UserId == userId && o.IsFavorite)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<OutfitSuggestion> CreateAsync(OutfitSuggestion outfit)
    {
        outfit.Id = Guid.NewGuid();
        outfit.CreatedAt = DateTime.UtcNow;
        
        _context.OutfitSuggestions.Add(outfit);
        await _context.SaveChangesAsync();
        return outfit;
    }

    public async Task<OutfitSuggestion> UpdateAsync(OutfitSuggestion outfit)
    {
        _context.OutfitSuggestions.Update(outfit);
        await _context.SaveChangesAsync();
        return outfit;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var outfit = await _context.OutfitSuggestions.FindAsync(id);
        if (outfit == null)
            return false;

        _context.OutfitSuggestions.Remove(outfit);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleFavoriteAsync(Guid id)
    {
        var outfit = await _context.OutfitSuggestions.FindAsync(id);
        if (outfit == null)
            return false;

        outfit.IsFavorite = !outfit.IsFavorite;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RateAsync(Guid id, int rating)
    {
        var outfit = await _context.OutfitSuggestions.FindAsync(id);
        if (outfit == null)
            return false;

        outfit.Rating = Math.Clamp(rating, 1, 5);
        await _context.SaveChangesAsync();
        return true;
    }
}
