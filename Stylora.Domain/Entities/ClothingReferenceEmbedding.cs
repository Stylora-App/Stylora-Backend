using Stylora.Domain.Enums;

namespace Stylora.Domain.Entities;

public class ClothingReferenceEmbedding : BaseEntity
{
    public Guid Id { get; set; }
    public ClothingReferenceLabel Label { get; set; }
    public string SourceKey { get; set; } = string.Empty;
    public string? CategoryHint { get; set; }
    public string? SourceDataset { get; set; }
    public string? GenderTag { get; set; }
    public string? MasterCategory { get; set; }
    public string? SubCategory { get; set; }
    public string? ArticleType { get; set; }
    public string? CategoryGroup { get; set; }
    public string? BaseColour { get; set; }
    public string? ColorFamily { get; set; }
    public string? SeasonTag { get; set; }
    public string? UsageTag { get; set; }
    public string? DisplayName { get; set; }
    public float[] Embedding { get; set; } = [];
    public bool IsActive { get; set; } = true;
}
