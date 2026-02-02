namespace Stylora.Domain.Entities;

/// <summary>
/// Junction table for many-to-many relationship between SeasonAnalysisResult and Color (3NF)
/// </summary>
public class RecommendedColor
{
    public Guid SeasonAnalysisResultId { get; set; }
    public SeasonAnalysisResult SeasonAnalysisResult { get; set; } = null!;
    
    public Guid ColorId { get; set; }
    public Color Color { get; set; } = null!;
}
