using Microsoft.EntityFrameworkCore;
using Stylora.Application.Interfaces;
using Stylora.Domain.Entities;
using Stylora.Infrastructure.Data;

namespace Stylora.Infrastructure.Repositories;

public class TryOnRepository : ITryOnRepository
{
    private readonly StyloraDbContext _context;

    public TryOnRepository(StyloraDbContext context)
    {
        _context = context;
    }

    public async Task<TryOnSession?> GetByIdAsync(Guid id)
    {
        return await _context.TryOnSessions
            .Include(t => t.WardrobeItem)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<IEnumerable<TryOnSession>> GetByUserIdAsync(Guid userId)
    {
        return await _context.TryOnSessions
            .Include(t => t.WardrobeItem)
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<TryOnSession> CreateAsync(TryOnSession session)
    {
        session.Id = Guid.NewGuid();
        session.CreatedAt = DateTime.UtcNow;
        
        _context.TryOnSessions.Add(session);
        await _context.SaveChangesAsync();
        return session;
    }

    public async Task<TryOnSession> UpdateAsync(TryOnSession session)
    {
        _context.TryOnSessions.Update(session);
        await _context.SaveChangesAsync();
        return session;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var session = await _context.TryOnSessions.FindAsync(id);
        if (session == null)
            return false;

        _context.TryOnSessions.Remove(session);
        await _context.SaveChangesAsync();
        return true;
    }
}
