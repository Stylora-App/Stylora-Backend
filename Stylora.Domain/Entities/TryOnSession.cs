namespace Stylora.Domain.Entities;

/// <summary>
/// Entity to store virtual try-on sessions for persistence and history
/// </summary>
public class TryOnSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    
    public string PersonImagePath { get; set; } = string.Empty;
    public string ClothingImagePath { get; set; } = string.Empty;
    public string? GeneratedImagePath { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    
    // Optional reference to wardrobe item if trying on from wardrobe
    public Guid? WardrobeItemId { get; set; }
    public WardrobeItem? WardrobeItem { get; set; }
}
