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

    // Mind-map annotation fields (not persisted to DB, only used in transient responses)
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? HairColor { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? HairDetail { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? EyeColor { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? EyeDetail { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? SkinTone { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? SkinDetail { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? Undertone { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? Contrast { get; set; }

    public string? AnalysisImagePath { get; set; }
    public string? ImageData { get; set; }
    public double? ConfidenceScore { get; set; }

    public ICollection<RecommendedColor> RecommendedColors { get; set; } = [];
}
