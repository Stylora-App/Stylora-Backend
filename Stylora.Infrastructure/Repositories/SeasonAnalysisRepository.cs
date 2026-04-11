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

    public async Task<SeasonAnalysisResult?> GetByUserIdAsync(Guid userId)
    {
        return await _context.SeasonAnalysisResults
            .Include(s => s.RecommendedColors)
                .ThenInclude(rc => rc.Color)
            .FirstOrDefaultAsync(s => s.UserId == userId);
    }

    public async Task<SeasonAnalysisResult> UpsertAsync(SeasonAnalysisResult analysis)
    {
        var existing = await _context.SeasonAnalysisResults
            .Include(s => s.RecommendedColors)
            .FirstOrDefaultAsync(s => s.UserId == analysis.UserId);

        if (existing != null)
        {
            existing.Season = analysis.Season;
            existing.SubSeason = analysis.SubSeason;
            existing.Description = analysis.Description;
            existing.BestMetals = analysis.BestMetals;
            existing.AnalysisImagePath = analysis.AnalysisImagePath;
            existing.ImageData = analysis.ImageData;
            existing.ConfidenceScore = analysis.ConfidenceScore;
            existing.UpdatedAt = DateTime.UtcNow;

            _context.RecommendedColors.RemoveRange(existing.RecommendedColors);
            existing.RecommendedColors.Clear();

            await AddColorsAsync(existing.Id, analysis.RecommendedColors);
            await _context.SaveChangesAsync();

            return await GetByIdAsync(existing.Id) ?? existing;
        }
        else
        {
            analysis.Id = Guid.NewGuid();
            analysis.CreatedAt = DateTime.UtcNow;

            var colors = analysis.RecommendedColors.ToList();
            analysis.RecommendedColors = [];

            _context.SeasonAnalysisResults.Add(analysis);
            await _context.SaveChangesAsync();

            await AddColorsAsync(analysis.Id, colors);
            await _context.SaveChangesAsync();

            return await GetByIdAsync(analysis.Id) ?? analysis;
        }
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

    private async Task AddColorsAsync(Guid analysisResultId, IEnumerable<RecommendedColor> recommendedColors)
    {
        foreach (var rc in recommendedColors)
        {
            var hexCode = rc.Color?.HexCode ?? rc.Color?.Name ?? "";
            if (string.IsNullOrEmpty(hexCode)) continue;

            var color = await _context.Colors.FirstOrDefaultAsync(c => c.HexCode == hexCode);
            if (color == null)
            {
                color = new Color { Id = Guid.NewGuid(), Name = hexCode, HexCode = hexCode };
                _context.Colors.Add(color);
                await _context.SaveChangesAsync();
            }

            _context.RecommendedColors.Add(new RecommendedColor
            {
                SeasonAnalysisResultId = analysisResultId,
                ColorId = color.Id
            });
        }
    }
}
