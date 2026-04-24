using Stylora.Domain.Enums;

namespace Stylora.Domain.Entities;

public class WardrobeItem : BaseEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string ImagePath { get; set; } = string.Empty;
    public ClothingCategory Category { get; set; }
    public StylePreference? Style { get; set; }

    public Guid? ColorId { get; set; }
    public Color? Color { get; set; }

    public int WornCount { get; set; } = 0;
    public ClothingValidationStatus? ValidationStatus { get; set; }
    public double? ValidationConfidence { get; set; }
    public string? ValidationMessage { get; set; }
    public DateTime? ValidatedAt { get; set; }

    public ICollection<TryOnSession> TryOnSessions { get; set; } = [];
}
