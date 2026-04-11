namespace Stylora.Domain.Entities;

public class TryOnSession : BaseEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string PersonImagePath { get; set; } = string.Empty;
    public string ClothingImagePath { get; set; } = string.Empty;
    public string? GeneratedImagePath { get; set; }

    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }

    public Guid? WardrobeItemId { get; set; }
    public WardrobeItem? WardrobeItem { get; set; }
}
