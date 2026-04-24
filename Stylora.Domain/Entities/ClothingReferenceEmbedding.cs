using Stylora.Domain.Enums;

namespace Stylora.Domain.Entities;

public class ClothingReferenceEmbedding : BaseEntity
{
    public Guid Id { get; set; }
    public ClothingReferenceLabel Label { get; set; }
    public string SourceKey { get; set; } = string.Empty;
    public string? CategoryHint { get; set; }
    public float[] Embedding { get; set; } = [];
    public bool IsActive { get; set; } = true;
}
