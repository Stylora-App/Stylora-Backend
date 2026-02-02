using Microsoft.EntityFrameworkCore;
using Stylora.Application.Interfaces;
using Stylora.Domain.Entities;
using Stylora.Infrastructure.Data;

namespace Stylora.Infrastructure.Repositories;

public class SeasonAnalysisRepository : ISeasonAnalysisRepository
{
    private readonly StyloraDbContext _context;

    public SeasonAnalysisRepository(StyloraDbContext context)
    {
        _context = context;
    }

    public async Task<SeasonAnalysisResult?> GetByIdAsync(Guid id)
    {
        return await _context.SeasonAnalysisResults
            .Include(s => s.RecommendedColors)
                .ThenInclude(rc => rc.Color)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<IEnumerable<SeasonAnalysisResult>> GetByUserIdAsync(Guid userId)
    {
        return await _context.SeasonAnalysisResults
            .Include(s => s.RecommendedColors)
                .ThenInclude(rc => rc.Color)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<SeasonAnalysisResult?> GetLatestByUserIdAsync(Guid userId)
    {
        return await _context.SeasonAnalysisResults
            .Include(s => s.RecommendedColors)
                .ThenInclude(rc => rc.Color)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<SeasonAnalysisResult> CreateAsync(SeasonAnalysisResult analysis)
    {
        analysis.Id = Guid.NewGuid();
        analysis.CreatedAt = DateTime.UtcNow;
        
        _context.SeasonAnalysisResults.Add(analysis);
        await _context.SaveChangesAsync();
        return analysis;
    }

    public async Task<SeasonAnalysisResult> UpdateAsync(SeasonAnalysisResult analysis)
    {
        _context.SeasonAnalysisResults.Update(analysis);
        await _context.SaveChangesAsync();
        return analysis;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var analysis = await _context.SeasonAnalysisResults.FindAsync(id);
        if (analysis == null)
            return false;

        _context.SeasonAnalysisResults.Remove(analysis);
        await _context.SaveChangesAsync();
        return true;
    }
}
