namespace Stylora.Domain.Entities;

public class SeasonAnalysisResult : BaseEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Season { get; set; } = string.Empty;
    public string SubSeason { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string BestMetals { get; set; } = string.Empty;

    public string? AnalysisImagePath { get; set; }
    public string? ImageData { get; set; }
    public double? ConfidenceScore { get; set; }

    public ICollection<RecommendedColor> RecommendedColors { get; set; } = [];
}
