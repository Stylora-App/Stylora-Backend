namespace Stylora.Domain.Entities;

public class SeasonAnalysisResult
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    
    public string Season { get; set; } = string.Empty;
    public string SubSeason { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string BestMetals { get; set; } = string.Empty;
    
    public string? AnalysisImagePath { get; set; }
    public string? ImageData { get; set; } // Base64 encoded image for reference
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public double? ConfidenceScore { get; set; }
    
    // Navigation property for recommended colors (3NF many-to-many)
    public ICollection<RecommendedColor> RecommendedColors { get; set; } = [];
}
